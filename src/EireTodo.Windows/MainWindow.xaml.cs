using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using EireTodo.Core;

namespace EireTodo.Windows;

public sealed record ProjectChoice(string Label, Guid? Value)
{
    public override string ToString() => Label;
}
public sealed record CategoryChoice(string Label, CategoryFilterMode Mode, string Value = "")
{
    public override string ToString() => Label;
}

public sealed class TaskRow
{
    public required TodoTask Task { get; init; }
    public required string Project { get; init; }
    public Guid Id => Task.Id;
    public string Description => Task.Description;
    public DateOnly? StartDate => Task.StartDate;
    public DateOnly? FinishDate => Task.FinishDate;
    public string StartDisplay => AustralianDates.Format(StartDate);
    public string FinishDisplay => AustralianDates.Format(FinishDate);
    public string Category => Task.Category;
    public string Notes => Task.Notes;
    public string NotesPreview
    {
        get
        {
            var text = string.Join(" ", Notes.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            return text.Length > 85 ? text[..85] + "…" : text;
        }
    }
    public DateTimeOffset CreatedAt => Task.CreatedAt;
    public string CreatedDisplay => AustralianDates.Format(CreatedAt);
    public bool Completed => Task.Completed;
    public bool IsOverdue => Overdue.IsDue(FinishDate, Completed);
    public FontFamily FontFamily => new(Task.Format.Family);
    public FontWeight FontWeight => Task.Format.Bold ? FontWeights.Bold : FontWeights.Normal;
    public FontStyle FontStyle => Task.Format.Italic ? FontStyles.Italic : FontStyles.Normal;
    public TextAlignment Alignment => Task.Format.Alignment == TextJustification.Centre ? TextAlignment.Center : Task.Format.Alignment == TextJustification.Right ? TextAlignment.Right : TextAlignment.Left;
    public TextDecorationCollection? Decorations => Task.Format.Underline ? TextDecorations.Underline : null;
    public double RowHeight => Math.Max(46, Task.Format.Size * 1.7 + 12);
}

public partial class MainWindow : Window
{
    private readonly TodoService service;
    private readonly string dataDirectory;
    private readonly ChartWorkspace chartWorkspace;
    private readonly FormatPainter painter = new();
    private readonly FormattingTools taskFormatTools;
    private readonly OverdueView overdueView;
    private readonly DispatcherTimer clockTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private DateTime lastOverdueRefresh;
    private bool overdueSaveFailed;
    private readonly DispatcherTimer settingsTimer = new() { Interval = TimeSpan.FromMilliseconds(350) };
    private readonly DispatcherTimer filterTimer = new() { Interval = TimeSpan.FromMilliseconds(220) };
    private bool initialized;
    private bool refreshing;
    private bool settingsDirty;
    private string sortMember = "CreatedAt";
    private ListSortDirection sortDirection = ListSortDirection.Descending;
    private TaskFilter appliedFilter = new();

    public MainWindow(TodoService service, string dataDirectory)
    {
        this.service = service;
        this.dataDirectory = dataDirectory;
        InitializeComponent();
        overdueView = new OverdueView(service, ToggleOverdue); OverdueHost.Content = overdueView;
        chartWorkspace = new ChartWorkspace(service, dataDirectory, painter, ToggleOverdue, RefreshOverdueView);
        var ribbon = new RibbonBar();
        var legacy = LegacyActionsCard.Child; LegacyActionsCard.Child = null; LegacyActionsCard.Visibility = Visibility.Collapsed;
        ribbon.Group("Home", "Tasks / projects / window").Children.Add(legacy);
        RibbonBar.Action(ribbon.Group("Insert", "Task"), "+ Add task", () => AddClick(this, new()), true);
        taskFormatTools = new FormattingTools(() => (TaskGrid.SelectedItem as TaskRow)?.Task.Format, ApplyTaskFormat, painter); ribbon.Group("Format", "Text").Children.Add(taskFormatTools);
        var view = ribbon.Group("View", "Data / window"); RibbonBar.Action(view, "Overdue log", ToggleOverdue); RibbonBar.Action(view, "Window settings", () => WindowOptionsClick(this, new())); RibbonBar.Action(view, "Clear filters", () => ClearFiltersClick(this, new()));
        TaskRibbonHost.Content = ribbon;
        TaskGrid.PreviewMouseLeftButtonUp += PaintTask;
        clockTimer.Tick += (_, _) => ClockTick(); ClockTick(); clockTimer.Start();
        ChartHost.Children.Add(chartWorkspace);
        settingsTimer.Tick += (_, _) => { settingsTimer.Stop(); SaveWindowSettings(); };
        filterTimer.Tick += (_, _) => { filterTimer.Stop(); ApplyFilters(); };
        ApplyWindowSettings();
        RefreshChoices();
        initialized = true;
        RefreshOverdueView();
        ApplyMode(false);
        ApplyFilters();
        LocationChanged += (_, _) => QueueSettings();
        SizeChanged += (_, _) => { UpdateControlsViewport(); QueueSettings(); };
        FooterPanel.SizeChanged += (_, _) => UpdateControlsViewport();
        SaveErrorPanel.SizeChanged += (_, _) => UpdateControlsViewport();
        foreach (var column in TaskGrid.Columns)
            DependencyPropertyDescriptor.FromProperty(DataGridColumn.WidthProperty, typeof(DataGridColumn))
                .AddValueChanged(column, (_, _) => QueueSettings());
        Loaded += (_, _) => { EnsureVisible(); UpdateControlsViewport(); };
        Closing += OnClosing;
    }

    private void UpdateControlsViewport()
    {
        if (!IsLoaded) return;
        OfflineBadge.Visibility = ActualWidth < 710 ? Visibility.Collapsed : Visibility.Visible;
        overdueView.MaxHeight = Math.Clamp(ActualHeight * .27, 75, 230);
        // Reserve space for the header, footer and several task rows as controls wrap.
        ControlsScroll.MaxHeight = Math.Max(48, ActualHeight - FooterPanel.ActualHeight - SaveErrorPanel.ActualHeight - OverdueHost.ActualHeight - 190);
    }

    private void ApplyWindowSettings()
    {
        var s = service.Data.Settings;
        ModeSelector.SelectedIndex = (int)s.ActiveMode;
        var virtualWidth = SystemParameters.VirtualScreenWidth;
        var virtualHeight = SystemParameters.VirtualScreenHeight;
        Width = Math.Clamp(s.Width, MinWidth, Math.Max(MinWidth, virtualWidth));
        Height = Math.Clamp(s.Height, MinHeight, Math.Max(MinHeight, virtualHeight));
        if (s.Left.HasValue && s.Top.HasValue) { Left = s.Left.Value; Top = s.Top.Value; }
        else { Left = SystemParameters.WorkArea.Left + 36; Top = SystemParameters.WorkArea.Top + 36; }
        OpacitySlider.Value = s.Opacity;
        TopToggle.IsChecked = s.AlwaysOnTop;
        Opacity = s.Opacity;
        Topmost = s.AlwaysOnTop;
        OpacityLabel.Text = $"{s.Opacity:P0}";
        foreach (var c in TaskGrid.Columns)
            if (s.ColumnWidths.TryGetValue(c.SortMemberPath, out var width)) c.Width = width;
    }

    private void EnsureVisible()
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (!GetWindowRect(handle, out var rect)) return;
        var monitor = MonitorFromRect(ref rect, 0);
        bool visible = false;
        if (monitor != IntPtr.Zero)
        {
            var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
            if (GetMonitorInfo(monitor, ref info))
                visible = rect.Top >= info.Work.Top && rect.Top < info.Work.Bottom - 28 &&
                    Math.Min(rect.Right, info.Work.Right) - Math.Max(rect.Left, info.Work.Left) >= 120;
        }
        if (!visible)
        {
            var area = SystemParameters.WorkArea;
            Width = Math.Min(Width, Math.Max(MinWidth, area.Width));
            Height = Math.Min(Height, Math.Max(MinHeight, area.Height));
            Left = area.Left + Math.Max(0, (area.Width - Width) / 2);
            Top = area.Top + Math.Max(0, (area.Height - Height) / 2);
            QueueSettings();
        }
    }

    private void RefreshChoices()
    {
        refreshing = true;
        var selectedProject = (ProjectFilter.SelectedItem as ProjectChoice)?.Value;
        var selectedCategory = CategoryFilter.SelectedItem as CategoryChoice;
        var projects = new List<ProjectChoice> { new("All projects", null) };
        projects.AddRange(service.Data.Projects.OrderBy(p => p.Archived).ThenBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase).Select(p => new ProjectChoice(p.DisplayName, p.Id)));
        ProjectFilter.ItemsSource = projects;
        ProjectFilter.SelectedItem = projects.FirstOrDefault(p => p.Value == selectedProject) ?? projects[0];
        var categories = new List<CategoryChoice> { new("All categories", CategoryFilterMode.Any), new("(Blank category)", CategoryFilterMode.Blank) };
        categories.AddRange(service.Data.Categories.Concat(service.Data.Tasks.Select(t => t.Category)).Where(c => c.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.CurrentCultureIgnoreCase).Select(c => new CategoryChoice(c, CategoryFilterMode.Value, c)));
        CategoryFilter.ItemsSource = categories;
        CategoryFilter.SelectedItem = categories.FirstOrDefault(c => c.Mode == selectedCategory?.Mode && c.Value == selectedCategory?.Value) ?? categories[0];
        refreshing = false;
    }

    private TaskFilter ReadFilters()
    {
        var category = CategoryFilter.SelectedItem as CategoryChoice;
        DateRange Range(ComboBox mode, TextBox from, TextBox to) => mode.SelectedIndex == 1
            ? new(DateFilterMode.Range, AustralianDates.ParseOptional(from.Text), AustralianDates.ParseOptional(to.Text))
            : new(mode.SelectedIndex == 2 ? DateFilterMode.Blank : DateFilterMode.Any);
        var filter = new TaskFilter
        {
            ProjectId = (ProjectFilter.SelectedItem as ProjectChoice)?.Value,
            TaskText = TaskSearch.Text.Trim(), NotesText = NotesSearch.Text.Trim(), BlankNotes = BlankNotes.IsChecked == true,
            CategoryMode = category?.Mode ?? CategoryFilterMode.Any, Category = category?.Value ?? "",
            Status = (StatusFilter)Math.Max(0, StatusFilterBox.SelectedIndex), HideCompleted = HideCompleted.IsChecked == true,
            Start = Range(StartMode, StartFrom, StartTo), Finish = Range(FinishMode, FinishFrom, FinishTo),
            Created = new(AustralianDates.ParseOptionalTime(CreatedFrom.Text), AustralianDates.ParseOptionalTime(CreatedTo.Text))
        };
        filter.Validate();
        return filter;
    }

    private void ApplyFilters()
    {
        if (!initialized || refreshing) return;
        try
        {
            appliedFilter = ReadFilters();
            FilterError.Visibility = Visibility.Collapsed;
            RefreshRows();
        }
        catch (ArgumentException ex)
        {
            FilterError.Text = ex.Message + " Previous valid filters remain applied.";
            FilterError.Visibility = Visibility.Visible;
            RefreshRows();
        }
    }

    private void RefreshRows()
    {
        var selectedId = (TaskGrid.SelectedItem as TaskRow)?.Id;
        var names = service.Data.Projects.ToDictionary(p => p.Id, p => p.DisplayName);
        var rows = service.Data.Tasks.Where(appliedFilter.Matches).Select(t => new TaskRow { Task = t, Project = names[t.ProjectId] }).ToList();
        TaskGrid.ItemsSource = rows;
        ApplySort();
        if (selectedId.HasValue) TaskGrid.SelectedItem = rows.FirstOrDefault(r => r.Id == selectedId);
        TaskCount.Text = $"{rows.Count} of {service.Data.Tasks.Count} tasks · {appliedFilter.ActiveCount} filters";
        EmptyMessage.Text = service.Data.Tasks.Count == 0 ? "No tasks yet. Add your first task." : "No tasks match these filters. Use Clear filters.";
        EmptyMessage.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }
    private void ApplySort()
    {
        var view = CollectionViewSource.GetDefaultView(TaskGrid.ItemsSource);
        if (view is null) return;
        view.SortDescriptions.Clear();
        view.SortDescriptions.Add(new SortDescription(sortMember, sortDirection));
        foreach (var column in TaskGrid.Columns) column.SortDirection = column.SortMemberPath == sortMember ? sortDirection : null;
    }
    private void GridSorting(object sender, DataGridSortingEventArgs e)
    {
        e.Handled = true;
        sortDirection = sortMember == e.Column.SortMemberPath && sortDirection == ListSortDirection.Ascending ? ListSortDirection.Descending : ListSortDirection.Ascending;
        sortMember = e.Column.SortMemberPath;
        ApplySort();
    }
    private void FilterChanged(object sender, RoutedEventArgs e) { if (initialized && !refreshing) ApplyFilters(); }
    private void FilterTextChanged(object sender, TextChangedEventArgs e)
    {
        if (!initialized || refreshing) return;
        filterTimer.Stop(); filterTimer.Start();
    }
    private void DateModeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!initialized) return;
        StartFrom.IsEnabled = StartTo.IsEnabled = StartMode.SelectedIndex == 1;
        FinishFrom.IsEnabled = FinishTo.IsEnabled = FinishMode.SelectedIndex == 1;
        FilterChanged(sender, e);
    }
    private void ClearFiltersClick(object sender, RoutedEventArgs e)
    {
        refreshing = true;
        ProjectFilter.SelectedIndex = CategoryFilter.SelectedIndex = StatusFilterBox.SelectedIndex = StartMode.SelectedIndex = FinishMode.SelectedIndex = 0;
        TaskSearch.Clear(); NotesSearch.Clear(); StartFrom.Clear(); StartTo.Clear(); FinishFrom.Clear(); FinishTo.Clear(); CreatedFrom.Clear(); CreatedTo.Clear();
        BlankNotes.IsChecked = HideCompleted.IsChecked = false;
        StartFrom.IsEnabled = StartTo.IsEnabled = FinishFrom.IsEnabled = FinishTo.IsEnabled = false;
        refreshing = false;
        filterTimer.Stop(); ApplyFilters();
    }

    private void AddClick(object sender, RoutedEventArgs e)
    {
        if (!service.Data.Projects.Any(p => !p.Archived))
        {
            MessageBox.Show(this, "Create or unarchive a project first.", "Add task", MessageBoxButton.OK, MessageBoxImage.Information);
            ProjectsClick(sender, e); return;
        }
        EditTask(null);
    }
    private void EditClick(object sender, RoutedEventArgs e) { if (TaskGrid.SelectedItem is TaskRow row) EditTask(row.Task); }
    private void EditTask(TodoTask? task)
    {
        var dialog = new TaskEditor(service, task, (ProjectFilter.SelectedItem as ProjectChoice)?.Value) { Owner = this };
        if (dialog.ShowDialog() == true) { RefreshChoices(); ApplyFilters(); RefreshOverdueView(); }
    }
    private void GridDoubleClick(object sender, MouseButtonEventArgs e)
    {
        var row = ItemsControl.ContainerFromElement(TaskGrid, e.OriginalSource as DependencyObject) as DataGridRow;
        if (row?.Item is TaskRow taskRow && !HasAncestor<CheckBox>(e.OriginalSource as DependencyObject)) EditTask(taskRow.Task);
    }
    private void GridSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        EditButton.IsEnabled = DeleteButton.IsEnabled = TaskGrid.SelectedItem is TaskRow;
        taskFormatTools?.Refresh();
    }
    private void GridKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Delete) { DeleteClick(sender, e); e.Handled = true; }
        else if (e.Key == Key.Enter) { EditClick(sender, e); e.Handled = true; }
    }
    private void CompletionClick(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox box && box.DataContext is TaskRow row)
        {
            TryAction(() => service.SetCompleted(row.Id, box.IsChecked == true));
            RefreshRows(); RefreshOverdueView();
            e.Handled = true;
        }
    }
    private void DeleteClick(object sender, RoutedEventArgs e)
    {
        if (TaskGrid.SelectedItem is not TaskRow row) return;
        var description = row.Description.Length > 150 ? row.Description[..150] + "…" : row.Description;
        if (MessageBox.Show(this, $"Delete this task?\n\n{description}", "Confirm deletion", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes)
        {
            if (TryAction(() => service.DeleteTask(row.Id))) { ApplyFilters(); RefreshOverdueView(); }
        }
    }
    private void ProjectsClick(object sender, RoutedEventArgs e)
    {
        new ProjectEditor(service) { Owner = this }.ShowDialog();
        RefreshChoices(); ApplyFilters(); chartWorkspace.RefreshData(); RefreshOverdueView();
    }
    private void ExportClick(object sender, RoutedEventArgs e)
    {
        if (!chartWorkspace.FinishInlineEdit()) return;
        if (settingsDirty && !SaveWindowSettings()) return;
        var dialog = new SaveFileDialog { Title = "Back up all projects, tasks, diagrams and window settings", Filter = "Eire backup (*.json)|*.json", FileName = $"EireTodo-backup-{DateTime.Now:yyyyMMdd-HHmm}.json", AddExtension = true, DefaultExt = ".json" };
        if (dialog.ShowDialog(this) == true && TryAction(() => service.Export(dialog.FileName)))
            MessageBox.Show(this, "Backup saved. It includes all projects, tasks, categories, diagrams and window settings.", "Backup complete", MessageBoxButton.OK, MessageBoxImage.Information);
    }
    private void RestoreClick(object sender, RoutedEventArgs e)
    {
        if (!chartWorkspace.FinishInlineEdit()) return;
        var dialog = new OpenFileDialog { Title = "Restore an Eire backup", Filter = "Eire backup (*.json)|*.json", CheckFileExists = true };
        if (dialog.ShowDialog(this) != true) return;
        if (MessageBox.Show(this, "Replace all current data and settings with this backup?\n\nA safety copy of the current data will be kept in the data folder.", "Confirm restore", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        settingsTimer.Stop();
        if (TryAction(() => service.Restore(dialog.FileName)))
        {
            settingsDirty = false;
            initialized = false;
            WindowState = WindowState.Normal;
            ApplyWindowSettings();
            RefreshChoices();
            initialized = true;
            chartWorkspace.RefreshData(true);
            ApplyMode(false); RefreshOverdueView();
            ClearFiltersClick(this, new RoutedEventArgs());
            EnsureVisible();
            SaveErrorPanel.Visibility = Visibility.Collapsed;
        }
        else if (settingsDirty) settingsTimer.Start();
    }
    private void DataFolderClick(object sender, RoutedEventArgs e) => TryAction(() => Process.Start(new ProcessStartInfo(dataDirectory) { UseShellExecute = true }));
    private bool TryAction(Action action)
    {
        try { action(); return true; }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"The operation could not be saved or completed. Your previous data remains available.\n\n{ex.Message}", "Operation failed", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }

    private static bool HasAncestor<T>(DependencyObject? source) where T : DependencyObject
    {
        for (var node = source; node is not null; node = node is System.Windows.Media.Visual or System.Windows.Media.Media3D.Visual3D
            ? System.Windows.Media.VisualTreeHelper.GetParent(node) : LogicalTreeHelper.GetParent(node))
            if (node is T) return true;
        return false;
    }
    private void HeaderDrag(object sender, MouseButtonEventArgs e)
    {
        if (HasAncestor<Button>(e.OriginalSource as DependencyObject) || HasAncestor<ComboBox>(e.OriginalSource as DependencyObject)) return;
        if (e.ClickCount == 2) { MaximizeClick(sender, e); return; }
        if (WindowState == WindowState.Maximized) WindowState = WindowState.Normal;
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }
    private void ModeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!initialized) return;
        if (!chartWorkspace.FinishInlineEdit()) { initialized = false; ModeSelector.SelectedIndex = (int)service.Data.Settings.ActiveMode; initialized = true; return; }
        ApplyMode(true); QueueSettings();
    }
    private void ApplyMode(bool changeLayout)
    {
        var mode = (AppMode)Math.Max(0, ModeSelector.SelectedIndex);
        var todo = mode == AppMode.Todo;
        ControlsScroll.Visibility = TodoTablePanel.Visibility = TaskCount.Visibility = todo ? Visibility.Visible : Visibility.Collapsed;
        ChartHost.Visibility = todo ? Visibility.Collapsed : Visibility.Visible;
        Title = mode == AppMode.Todo ? "Eire To-do" : mode == AppMode.MindMap ? "Eire Mind map" : mode == AppMode.Wbs ? "Eire WBS chart" : "Eire Connections";
        if (!todo) chartWorkspace.SetMode(mode, changeLayout);
        UpdateControlsViewport();
    }
    private void WindowOptionsClick(object sender, RoutedEventArgs e)
    {
        var dialog = new Window { Title = "Window settings", Width = 425, Height = 245, MinWidth = 350, MinHeight = 220, Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        Ui.ApplyWindowStyle(dialog);
        var panel = new StackPanel { Margin = new Thickness(18) };
        var top = new CheckBox { Content = "Always on top", IsChecked = TopToggle.IsChecked, Margin = new Thickness(0,0,0,14) };
        top.Checked += (_, _) => TopToggle.IsChecked = true; top.Unchecked += (_, _) => TopToggle.IsChecked = false;
        var slider = new Slider { Minimum = .65, Maximum = 1, Value = OpacitySlider.Value, TickFrequency = .01, IsSnapToTickEnabled = true, Margin = new Thickness(0,8,0,12) };
        var label = Ui.Label($"Window opacity · {slider.Value:P0}");
        slider.ValueChanged += (_, _) => { OpacitySlider.Value = slider.Value; label.Text = $"Window opacity · {slider.Value:P0}"; };
        panel.Children.Add(top); panel.Children.Add(label); panel.Children.Add(slider);
        panel.Children.Add(Ui.Button("Done", (_, _) => dialog.Close(), true)); dialog.Content = panel; dialog.ShowDialog();
    }
    private void MinimizeClick(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void MaximizeClick(object sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private void CloseClick(object sender, RoutedEventArgs e) => Close();
    private void WindowSettingChanged(object sender, RoutedEventArgs e)
    {
        if (!initialized) return;
        Topmost = TopToggle.IsChecked == true; QueueSettings();
    }
    private void OpacityChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!initialized) return;
        Opacity = OpacitySlider.Value;
        OpacityLabel.Text = $"{Opacity:P0}";
        QueueSettings();
    }
    private void QueueSettings()
    {
        if (!initialized || !IsLoaded) return;
        settingsDirty = true;
        settingsTimer.Stop(); settingsTimer.Start();
    }
    private bool SaveWindowSettings()
    {
        if (!initialized || !settingsDirty) return true;
        var bounds = WindowState == WindowState.Normal ? new Rect(Left, Top, ActualWidth, ActualHeight) : RestoreBounds;
        if (bounds.IsEmpty) return true;
        var settings = new WindowSettings
        {
            Left = bounds.Left, Top = bounds.Top, Width = Math.Max(MinWidth, bounds.Width), Height = Math.Max(MinHeight, bounds.Height),
            Opacity = OpacitySlider.Value, AlwaysOnTop = TopToggle.IsChecked == true,
            ActiveMode = (AppMode)Math.Max(0, ModeSelector.SelectedIndex), SelectedDiagramId = service.Data.Settings.SelectedDiagramId, SelectedNetworkId = service.Data.Settings.SelectedNetworkId, ShowOverdueLog = service.Data.Settings.ShowOverdueLog,
            ColumnWidths = TaskGrid.Columns.ToDictionary(c => c.SortMemberPath, c => Math.Clamp(c.ActualWidth, 35, 3000))
        };
        try
        {
            service.SaveSettings(settings);
            settingsDirty = false;
            if (!overdueSaveFailed) SaveErrorPanel.Visibility = Visibility.Collapsed;
            return true;
        }
        catch (Exception ex)
        {
            SaveErrorText.Text = "Window settings were not saved. " + ex.Message;
            SaveErrorPanel.Visibility = Visibility.Visible;
            return false;
        }
    }
    private void RetrySettingsClick(object sender, RoutedEventArgs e) { SaveWindowSettings(); lastOverdueRefresh = default; ClockTick(); }
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (!chartWorkspace.FinishInlineEdit()) { e.Cancel = true; return; }
        settingsTimer.Stop(); filterTimer.Stop(); clockTimer.Stop();
        if (!SaveWindowSettings())
        {
            var close = MessageBox.Show(this, "Window settings could not be saved. Task changes already saved remain safe.\n\nClose without saving these window settings?", "Unsaved window settings", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
            e.Cancel = close != MessageBoxResult.Yes;
            if (e.Cancel) clockTimer.Start();
        }
    }
    private void ApplyTaskFormat(TextFormat format)
    {
        if (TaskGrid.SelectedItem is not TaskRow row) return;
        if (TryAction(() => service.Change(d => d.Tasks.Single(t => t.Id == row.Id).Format = format.Clone()))) { RefreshRows(); taskFormatTools.Refresh(); }
    }
    private void PaintTask(object sender, MouseButtonEventArgs e)
    {
        if (painter.Copied is not TextFormat format || HasAncestor<CheckBox>(e.OriginalSource as DependencyObject)) return;
        var container = ItemsControl.ContainerFromElement(TaskGrid, e.OriginalSource as DependencyObject) as DataGridRow;
        if (container?.Item is TaskRow row && TryAction(() => service.Change(d => d.Tasks.Single(t => t.Id == row.Id).Format = format.Clone()))) { painter.Clear(); RefreshRows(); }
    }
    private void ToggleOverdueClick(object sender, RoutedEventArgs e) => ToggleOverdue();
    private void ToggleOverdue()
    {
        if (TryAction(() => service.Change(d => d.Settings.ShowOverdueLog = !d.Settings.ShowOverdueLog))) { RefreshOverdueView(); UpdateControlsViewport(); }
    }
    private void RefreshOverdueView()
    {
        overdueView.Refresh(); OverdueHost.Visibility = service.Data.Settings.ShowOverdueLog ? Visibility.Visible : Visibility.Collapsed;
        var active = service.Data.OverdueLog.Count(e => e.Resolution == OverdueResolution.Active);
        OverdueButton.Content = (service.Data.Settings.ShowOverdueLog ? "Hide" : "Show") + $" overdue log ({active})";
    }
    private void ClockTick()
    {
        HeaderClock.Text = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss", AustralianDates.Culture);
        if (!initialized || DateTime.Now - lastOverdueRefresh < TimeSpan.FromMinutes(1)) return;
        lastOverdueRefresh = DateTime.Now;
        try
        {
            var updated = service.RefreshOverdue();
            if (updated) { RefreshRows(); chartWorkspace.RefreshIfIdle(); }
            if (overdueSaveFailed && !settingsDirty) SaveErrorPanel.Visibility = Visibility.Collapsed;
            overdueSaveFailed = false; RefreshOverdueView();
        }
        catch (Exception ex) { overdueSaveFailed = true; SaveErrorText.Text = "Overdue history could not be saved. " + ex.Message; SaveErrorPanel.Visibility = Visibility.Visible; }
    }
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { public int Size; public NativeRect Monitor; public NativeRect Work; public uint Flags; }
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rect);
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromRect(ref NativeRect rect, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Auto)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
}
