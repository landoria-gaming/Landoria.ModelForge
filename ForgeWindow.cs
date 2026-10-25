using System;
using System.IO;
using BepInEx.Logging;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Landoria.ModelForge
{
    // Controls local model actions inside Valheim's native popup.
    internal sealed class ForgeWindow
    {
        private readonly ForgeSession _session;
        private readonly ManualLogSource _log;
        private ForgeUiFactory _ui;
        private ForgeDropdown _dropdown;
        private NativePopupHost _host;
        private TMP_Text _status;
        private TMP_Text _targetName;
        private TMP_Text _selection;
        private Button _export;
        private Button _import;
        private Button _erase;
        private CursorLockMode _previousLock;
        private bool _previousRequested;
        private string[] _files = Array.Empty<string>();
        private int _selected;
        internal bool IsOpen => _host != null && _host.IsOpen;

        // Stores the model session and logger.
        internal ForgeWindow(ForgeSession session, ManualLogSource log)
        {
            _session = session;
            _log = log;
        }

        // Opens or closes the dialog with the configured shortcut.
        internal void Toggle()
        {
            if (IsOpen) { Close(); return; }
            _session.CaptureTarget();
            _previousLock = ZCursor.LockState;
            _previousRequested = ZCursor.IsRequested;
            var host = new NativePopupHost(_log);
            if (!host.Open(out var panel, out var sample)) { return; }
            _host = host;
            _ui = new ForgeUiFactory(sample);
            BuildContents(panel);
            Refresh();
            UpdateCursor();
        }

        // Hides the dialog and restores the previous cursor state.
        internal void Close()
        {
            if (!IsOpen) { return; }
            _host.Close();
            _host = null;
            ZCursor.LockState = _previousLock;
            if (_previousRequested) { ZCursor.Show(); }
            else { ZCursor.Hide(); }
        }

        // Keeps UI input separate from Valheim's mouse controls.
        internal void UpdateCursor()
        {
            if (!IsOpen) { return; }
            ZCursor.LockState = CursorLockMode.None;
            ZCursor.Show();
            ZInput.IgnoreMouseInputForFrames(2);
            if (Input.GetKeyDown(KeyCode.Escape)) { Close(); }
        }

        // Removes the temporary Unity UI objects on plugin shutdown.
        internal void Dispose()
        {
            Close();
        }

        // Adds model actions and the file list inside the native frame.
        private void BuildContents(RectTransform panel)
        {
            _targetName = _ui.Label(panel, "", 0, -78, 520, 28, 18, TextAlignmentOptions.Center);
            _export = _ui.Button(panel, "Export selected model + JPEG", -132, Export);
            _selection = _ui.Label(panel, "No GLB files", 0, -188, 440, 36, 20, TextAlignmentOptions.Center);
            _ui.Button(panel, "Choose model", -230, ToggleDropdown);
            _dropdown = new ForgeDropdown(_ui, panel, -267, SelectModel);
            _import = _ui.Button(panel, "Import selected model", -429, Import);
            _erase = _ui.Button(panel, "Erase selected imported model", -477, Erase);
            _ui.Button(panel, "Close", -525, Close);
            _status = _ui.Label(panel, "", 0, -565, 520, 44, 17, TextAlignmentOptions.Center);
        }

        // Updates the selector after a file is created or removed.
        private void Refresh()
        {
            _files = Directory.GetFiles(_session.Folder, "*.glb");
            Array.Sort(_files, StringComparer.OrdinalIgnoreCase);
            _selected = Mathf.Clamp(_selected, 0, Math.Max(0, _files.Length - 1));
            _targetName.text = "Target: " + _session.TargetName;
            _export.interactable = _session.HasTarget;
            _erase.interactable = _session.CanEraseTarget;
            _selection.text = _files.Length == 0 ? "No GLB files" : Path.GetFileName(_files[_selected]);
            _import.interactable = _files.Length > 0;
            _dropdown.SetFiles(_files);
        }

        // Shows or hides the scrollable model choices.
        private void ToggleDropdown()
        {
            Refresh();
            _dropdown.Toggle();
        }

        // Saves the selected model index from the dropdown.
        private void SelectModel(int index)
        {
            _selected = index;
            _selection.text = Path.GetFileName(_files[index]);
            _dropdown.Hide();
        }

        // Exports the object captured when the dialog opened.
        private void Export()
        {
            Run(_session.ExportTarget);
        }

        // Starts a temporary placement of the chosen GLB.
        private void Import()
        {
            if (_files.Length == 0) { return; }
            if (Run(() => _session.BeginPlacement(_files[_selected]))) { Close(); }
        }

        // Erases only a previously imported model.
        private void Erase()
        {
            Run(_session.EraseTarget);
        }

        // Displays operation results and logs unexpected failures.
        private bool Run(Func<string> action)
        {
            try
            {
                _status.text = action();
                Refresh();
                return true;
            }
            catch (Exception error)
            {
                _status.text = error.Message;
                _log.LogWarning(error);
                return false;
            }
        }
    }
}
