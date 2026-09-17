using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ClarityGameOptimizer.UI
{
    /// <summary>
    /// The one window: the area rail on the left, the selected area's cards and findings on the right,
    /// Scan, Export and Render in the toolbar. Scan results live in the view model for the session only;
    /// a domain reload starts the window empty, because a recompile can invalidate what a scan read.
    /// </summary>
    internal sealed class ClarityGameOptimizerWindow : EditorWindow
    {
        public const string Title = "Clarity Game Optimizer";

        private OptimizerViewModel _model;
        private OptimizerView _view;

        [MenuItem("Window/Clarity Game Optimizer")]
        public static void Open()
        {
            var window = GetWindow<ClarityGameOptimizerWindow>();
            window.titleContent = new GUIContent(Title);
            window.minSize = new Vector2(900, 480);
            window.Show();
        }

        private void CreateGUI()
        {
            titleContent = new GUIContent(Title);
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(OptimizerView.StyleSheetPath);
            if (styleSheet != null)
            {
                rootVisualElement.styleSheets.Add(styleSheet);
            }
            else
            {
                Debug.LogWarning(ReportActions.LogPrefix + "Style sheet not found at " + OptimizerView.StyleSheetPath + "; the window runs unstyled.");
            }

            _model = new OptimizerViewModel();
            _view = new OptimizerView(rootVisualElement, _model);
        }

        private void OnDisable()
        {
            if (_view != null)
            {
                _view.Dispose();
                _view = null;
            }
        }
    }
}
