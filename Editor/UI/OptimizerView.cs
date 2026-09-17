using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ClarityGameOptimizer.Analysis;
using ClarityGameOptimizer.Core;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace ClarityGameOptimizer.UI
{
    /// <summary>
    /// The window's visual tree, built in C# on a root the caller provides so it can be exercised without
    /// a panel: a toolbar with the scope, Scan, Export and Render; a rail of the seven areas with their
    /// finding counts; summary cards; the findings list, most severe first, with a detail line; and a
    /// status bar. Everything it shows comes from the view model; everything it does goes through
    /// <see cref="ReportActions"/>.
    /// </summary>
    internal sealed class OptimizerView
    {
        public const string StyleSheetPath = "Packages/com.alvaris.clarity-game-optimizer/Editor/UI/ClarityGameOptimizer.uss";
        public const int RowHeight = 22;

        private static readonly string[] SeverityClasses = { "severity-info", "severity-advice", "severity-warning", "severity-critical" };

        private readonly OptimizerViewModel _model;
        private readonly Dictionary<Area, Button> _railButtons = new Dictionary<Area, Button>();
        private readonly Dictionary<Area, Label> _railCounts = new Dictionary<Area, Label>();
        private readonly List<Finding> _rows = new List<Finding>();

        private DropdownField _scope;
        private ToolbarButton _scan;
        private ToolbarButton _render;
        private VisualElement _cards;
        private Label _empty;
        private MultiColumnListView _list;
        private Label _detail;
        private Label _status;
        private Label _archifyStatus;
        private WrittenFiles? _written;

        public OptimizerView(VisualElement root, OptimizerViewModel model)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            _model = model ?? throw new ArgumentNullException(nameof(model));
            Root = root;
            Build(root);
            _model.Changed += Refresh;
            Refresh();
        }

        public VisualElement Root { get; }

        public MultiColumnListView List
        {
            get { return _list; }
        }

        public string StatusText
        {
            get { return _status.text; }
        }

        public void Dispose()
        {
            _model.Changed -= Refresh;
        }

        public void SelectArea(Area area)
        {
            _model.SelectedArea = area;
        }

        /// <summary>Scans the selected area and shows the result; failures go to the console and the status bar.</summary>
        public void Scan()
        {
            AreaDescriptor descriptor = _model.SelectedDescriptor;
            if (!descriptor.IsAvailable)
            {
                return;
            }

            try
            {
                _written = null;
                ReportActions.Scan(_model, descriptor);
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogException(exception);
                SetStatus("Scan failed: " + exception.Message + " (details in the console).");
            }
        }

        /// <summary>Writes every file for the current report into its report folder.</summary>
        public void WriteAll()
        {
            Report report = _model.Current;
            if (report == null)
            {
                SetStatus("Scan first.");
                return;
            }

            try
            {
                _written = ReportActions.WriteAll(report);
                SetStatus("Files written to " + _written.Value.Folder);
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogException(exception);
                SetStatus("Writing failed: " + exception.Message + " (details in the console).");
            }
        }

        /// <summary>Scans if needed, writes the files, renders the diagram with archify and opens it.</summary>
        public void Render()
        {
            if (_model.Current == null)
            {
                Scan();
                if (_model.Current == null)
                {
                    return;
                }
            }

            WriteAll();
            if (!_written.HasValue)
            {
                return;
            }

            try
            {
                string html;
                string outcome = ReportActions.Render(_written.Value, out html);
                SetStatus(outcome);
                if (html != null)
                {
                    ReportActions.OpenLocal(html);
                }
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogException(exception);
                SetStatus("Rendering failed: " + exception.Message + " (details in the console).");
            }
        }

        private void Build(VisualElement root)
        {
            root.AddToClassList("cgo-root");
            root.Add(BuildToolbar());

            var body = new VisualElement { name = "body" };
            body.AddToClassList("cgo-body");
            body.Add(BuildRail());
            body.Add(BuildContent());
            root.Add(body);

            root.Add(BuildStatusBar());
        }

        private Toolbar BuildToolbar()
        {
            var toolbar = new Toolbar { name = "toolbar" };
            var scopeLabel = new Label("Scope");
            scopeLabel.AddToClassList("cgo-toolbar-label");
            toolbar.Add(scopeLabel);

            _scope = new DropdownField(new List<string> { AssemblyGraphReport.Scope }, 0) { name = "scope" };
            _scope.AddToClassList("cgo-scope");
            toolbar.Add(_scope);

            _scan = new ToolbarButton(Scan) { name = "scan", text = "Scan", tooltip = "Read the project and list the findings" };
            _scan.AddToClassList("cgo-toolbar-button");
            toolbar.Add(_scan);

            toolbar.Add(new ToolbarSpacer { flex = true });

            var export = new ToolbarMenu { name = "export", text = "Export" };
            export.AddToClassList("cgo-toolbar-button");
            export.menu.AppendAction("Write report and diagram files", action => WriteAll(), HasReport);
            export.menu.AppendAction("Open report folder", action => OpenReportFolder(), HasReport);
            export.menu.AppendSeparator();
            export.menu.AppendAction("Save report as JSON...", action => SaveAs("report", "json", ReportJsonExporter.Write(_model.Current) + "\n"), HasReport);
            export.menu.AppendAction("Save report as Markdown...", action => SaveAs("report", "md", ReportMarkdownExporter.Write(_model.Current)), HasReport);
            export.menu.AppendAction("Save diagram as archify JSON...", action => SaveAs(Areas.DiagramBaseName(_model.SelectedArea) + ".architecture", "json", ArchifyArchitectureExporter.Export(_model.Current) + "\n"), HasGraph);
            export.menu.AppendAction("Save diagram as DOT...", action => SaveAs(Areas.DiagramBaseName(_model.SelectedArea), "dot", DotExporter.Write(_model.Current.Graph, Areas.DiagramTitle(_model.SelectedArea))), HasGraph);
            export.menu.AppendAction("Save diagram as Mermaid...", action => SaveAs(Areas.DiagramBaseName(_model.SelectedArea), "mmd", MermaidExporter.Write(_model.Current.Graph)), HasGraph);
            export.menu.AppendSeparator();
            export.menu.AppendAction("Locate archify...", action => LocateArchify());
            toolbar.Add(export);

            _render = new ToolbarButton(Render) { name = "render", text = "Render", tooltip = "Write the files, render the diagram with archify and open it" };
            _render.AddToClassList("cgo-toolbar-button");
            toolbar.Add(_render);
            return toolbar;
        }

        private VisualElement BuildRail()
        {
            var rail = new ScrollView { name = "rail" };
            rail.AddToClassList("cgo-rail");
            foreach (AreaDescriptor descriptor in AreaCatalog.All)
            {
                Area area = descriptor.Area;
                var button = new Button(() => SelectArea(area)) { name = "rail-" + Areas.FolderName(area), tooltip = descriptor.Question };
                button.AddToClassList("cgo-rail-item");
                if (!descriptor.IsAvailable)
                {
                    button.AddToClassList("cgo-rail-item--planned");
                    button.tooltip += " " + descriptor.PlannedFor + ".";
                }

                var title = new Label(descriptor.Title);
                title.AddToClassList("cgo-rail-title");
                button.Add(title);
                var count = new Label("");
                count.AddToClassList("cgo-rail-count");
                button.Add(count);
                rail.Add(button);
                _railButtons.Add(area, button);
                _railCounts.Add(area, count);
            }

            return rail;
        }

        private VisualElement BuildContent()
        {
            var content = new VisualElement { name = "content" };
            content.AddToClassList("cgo-content");

            _cards = new VisualElement { name = "cards" };
            _cards.AddToClassList("cgo-cards");
            content.Add(_cards);

            _empty = new Label { name = "empty" };
            _empty.AddToClassList("cgo-empty");
            content.Add(_empty);

            _list = BuildList();
            content.Add(_list);

            _detail = new Label { name = "detail" };
            _detail.AddToClassList("cgo-detail");
            content.Add(_detail);
            return content;
        }

        private MultiColumnListView BuildList()
        {
            var list = new MultiColumnListView
            {
                name = "findings",
                fixedItemHeight = RowHeight,
                virtualizationMethod = CollectionVirtualizationMethod.FixedHeight,
                selectionType = SelectionType.Single,
                showBorder = true,
                showAlternatingRowBackgrounds = AlternatingRowBackground.ContentOnly,
            };
            list.AddToClassList("cgo-list");
            list.columns.Add(TextColumn("severity", "Severity", 80, false, finding => finding.Severity.ToString(), true));
            list.columns.Add(TextColumn("subject", "Subject", 230, false, finding => finding.Subject, false));
            list.columns.Add(TextColumn("title", "Finding", 320, true, finding => finding.Title, false));
            list.columns.Add(TextColumn("measured", "Measured", 110, false, Measured, false));
            list.columns.Add(TextColumn("verdict", "Verdict", 280, false, finding => finding.Verdict, false));
            list.itemsSource = _rows;
            list.selectionChanged += selection => ShowDetail(list.selectedIndex);
            list.itemsChosen += chosen =>
            {
                foreach (object item in chosen)
                {
                    var finding = item as Finding;
                    if (finding != null && !ReportActions.PingEvidence(finding))
                    {
                        SetStatus("No asset to show for " + finding.Subject + ".");
                    }
                }
            };
            return list;
        }

        private Column TextColumn(string name, string title, int width, bool stretch, Func<Finding, string> text, bool severityColours)
        {
            var column = new Column
            {
                name = name,
                title = title,
                width = width,
                minWidth = 60,
                stretchable = stretch,
                makeCell = () =>
                {
                    var label = new Label();
                    label.AddToClassList("cgo-cell");
                    return label;
                },
            };
            column.bindCell = (element, index) =>
            {
                var label = (Label)element;
                Finding finding = _rows[index];
                label.text = text(finding);
                label.tooltip = label.text;
                if (severityColours)
                {
                    foreach (string severityClass in SeverityClasses)
                    {
                        label.RemoveFromClassList(severityClass);
                    }

                    label.AddToClassList(SeverityClasses[(int)finding.Severity]);
                }
            };
            return column;
        }

        private VisualElement BuildStatusBar()
        {
            var bar = new VisualElement { name = "status-bar" };
            bar.AddToClassList("cgo-status-bar");
            _status = new Label { name = "status" };
            _status.AddToClassList("cgo-status");
            bar.Add(_status);
            _archifyStatus = new Label(ReportActions.ArchifyStatus()) { name = "archify-status" };
            _archifyStatus.AddToClassList("cgo-status-right");
            bar.Add(_archifyStatus);
            return bar;
        }

        private void Refresh()
        {
            AreaDescriptor descriptor = _model.SelectedDescriptor;
            Report report = _model.Current;

            foreach (KeyValuePair<Area, Button> entry in _railButtons)
            {
                entry.Value.EnableInClassList("cgo-rail-item--active", entry.Key == _model.SelectedArea);
                int? count = _model.FindingCount(entry.Key);
                _railCounts[entry.Key].text = count.HasValue ? count.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : "";
                _railCounts[entry.Key].EnableInClassList("cgo-rail-count--empty", !count.HasValue);
            }

            _scope.choices = new List<string>(descriptor.Scopes.Count > 0 ? descriptor.Scopes : new[] { "" });
            _scope.index = 0;
            _scope.SetEnabled(descriptor.Scopes.Count > 1);
            _scan.SetEnabled(descriptor.IsAvailable);
            _render.SetEnabled(descriptor.IsAvailable);

            _cards.Clear();
            foreach (Metric metric in _model.HeadlineMetrics)
            {
                var card = new VisualElement();
                card.AddToClassList("cgo-card");
                var value = new Label(metric.Value.ToString());
                value.AddToClassList("cgo-card-value");
                card.Add(value);
                var label = new Label(metric.Label) { tooltip = metric.Key };
                label.AddToClassList("cgo-card-label");
                card.Add(label);
                _cards.Add(card);
            }

            _rows.Clear();
            _rows.AddRange(_model.Findings);
            _list.RefreshItems();
            _list.ClearSelection();
            _detail.text = "";

            bool showList = report != null;
            _list.style.display = showList ? DisplayStyle.Flex : DisplayStyle.None;
            _cards.style.display = showList ? DisplayStyle.Flex : DisplayStyle.None;
            _detail.style.display = showList ? DisplayStyle.Flex : DisplayStyle.None;
            _empty.style.display = showList ? DisplayStyle.None : DisplayStyle.Flex;
            _empty.text = descriptor.Question + "\n\n" + (descriptor.IsAvailable ? "Press Scan to read the project." : descriptor.PlannedFor + ".");
            SetStatus(_model.ScanSummary);
        }

        private void ShowDetail(int index)
        {
            if (index < 0 || index >= _rows.Count)
            {
                _detail.text = "";
                return;
            }

            Finding finding = _rows[index];
            var text = new StringBuilder();
            text.Append(finding.Severity).Append(": ").Append(finding.Title);
            if (finding.Verdict.Length > 0)
            {
                text.Append("\nVerdict: ").Append(finding.Verdict);
            }

            if (finding.Measured.HasValue)
            {
                text.Append("\nMeasured: ").Append(finding.Measured.Value);
                if (finding.Projected.HasValue)
                {
                    text.Append(" → ").Append(finding.Projected.Value);
                }

                text.Append(" (").Append(Budgets.Describe(finding.Budget)).Append(')');
            }

            if (finding.Evidence.Count > 0)
            {
                text.Append("\nEvidence: ");
                for (int i = 0; i < finding.Evidence.Count; i++)
                {
                    text.Append(i == 0 ? "" : ", ").Append(finding.Evidence[i].Locator);
                }

                text.Append("  (double-click the row to show it)");
            }

            _detail.text = text.ToString();
        }

        private static string Measured(Finding finding)
        {
            if (!finding.Measured.HasValue)
            {
                return "";
            }

            string measured = finding.Measured.Value.ToString();
            return finding.Projected.HasValue ? measured + " → " + finding.Projected.Value : measured;
        }

        private DropdownMenuAction.Status HasReport(DropdownMenuAction action)
        {
            return _model.Current != null ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled;
        }

        private DropdownMenuAction.Status HasGraph(DropdownMenuAction action)
        {
            return _model.Current != null && !_model.Current.Graph.IsEmpty ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled;
        }

        private void OpenReportFolder()
        {
            Report report = _model.Current;
            if (report == null)
            {
                return;
            }

            string folder = ReportFolders.ForArea(report.Area);
            Directory.CreateDirectory(folder);
            ReportActions.OpenLocal(folder);
        }

        private void SaveAs(string defaultName, string extension, string text)
        {
            string path = EditorUtility.SaveFilePanel("Save " + defaultName + "." + extension, ReportFolders.Root, defaultName, extension);
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            File.WriteAllText(path, text, new UTF8Encoding(false));
            SetStatus("Saved " + path);
        }

        private void LocateArchify()
        {
            string outcome = ReportActions.LocateArchify();
            if (outcome != null)
            {
                SetStatus(outcome);
            }

            _archifyStatus.text = ReportActions.ArchifyStatus();
        }

        private void SetStatus(string text)
        {
            _status.text = text ?? "";
        }
    }
}
