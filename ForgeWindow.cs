using System;
using System.IO;
using UnityEngine;

namespace Landoria.ModelForge
{
    // Draws a small export, import, and erase dialog.
    internal sealed class ForgeWindow
    {
        private readonly ForgeSession _session;
        private Rect _rect = new Rect(30, 80, 380, 330);
        private Vector2 _scroll;
        private CursorLockMode _previousLock;
        private bool _previousVisible;
        private string[] _files = Array.Empty<string>();
        private int _selected;
        private bool _open;
        private bool _listOpen;
        private string _message = "";

        // Stores the active session.
        internal ForgeWindow(ForgeSession session)
        {
            _session = session;
        }

        // Toggles the dialog and refreshes its model list.
        internal void Toggle()
        {
            if (!_open)
            {
                _previousLock = Cursor.lockState;
                _previousVisible = Cursor.visible;
            }
            _open = !_open;
            if (!_open) { RestoreCursor(); }
            _listOpen = false;
            Refresh();
        }

        // Closes the dialog when the plugin unloads.
        internal void Close()
        {
            if (_open)
            {
                _open = false;
                RestoreCursor();
            }
        }

        // Keeps the mouse available while the game dialog is open.
        internal void UpdateCursor()
        {
            if (_open)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        // Restores the cursor state that preceded the dialog.
        private void RestoreCursor()
        {
            Cursor.lockState = _previousLock;
            Cursor.visible = _previousVisible;
        }

        // Finds importable GLB files in the working folder.
        private void Refresh()
        {
            _files = Directory.GetFiles(_session.Folder, "*.glb");
            Array.Sort(_files, StringComparer.OrdinalIgnoreCase);
            _selected = Mathf.Clamp(_selected, 0, Math.Max(0, _files.Length - 1));
        }

        // Draws the dialog only while it is open.
        internal void Draw()
        {
            if (_open)
            {
                _rect = GUI.Window(78431, _rect, DrawContents, "ModelForge");
            }
        }

        // Draws the actions and the import dropdown.
        private void DrawContents(int id)
        {
            GUILayout.Label("Models: " + _session.Folder);
            if (GUILayout.Button("Export object under cursor + JPEG"))
            {
                Run(_session.ExportTarget);
            }
            var label = _files.Length == 0 ? "No GLB files" : Path.GetFileName(_files[_selected]);
            if (GUILayout.Button("Model: " + label))
            {
                Refresh();
                _listOpen = !_listOpen;
            }
            if (_listOpen)
            {
                DrawChoices();
            }
            GUI.enabled = _files.Length > 0;
            if (GUILayout.Button("Import selected model"))
            {
                Run(() => _session.BeginPlacement(_files[_selected]));
                _open = false;
                RestoreCursor();
            }
            GUI.enabled = true;
            if (GUILayout.Button("Erase imported model under cursor"))
            {
                Run(_session.EraseTarget);
            }
            GUILayout.Label(_message);
            if (GUILayout.Button("Close"))
            {
                _open = false;
                RestoreCursor();
            }
            GUI.DragWindow(new Rect(0, 0, 360, 24));
        }

        // Shows each available model as a dropdown choice.
        private void DrawChoices()
        {
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(130));
            for (var i = 0; i < _files.Length; i++)
            {
                if (GUILayout.Button(Path.GetFileName(_files[i])))
                {
                    _selected = i;
                    _listOpen = false;
                }
            }
            GUILayout.EndScrollView();
        }

        // Converts expected operation errors into a visible message.
        private void Run(Func<string> action)
        {
            try
            {
                _message = action();
                Refresh();
            }
            catch (Exception error)
            {
                _message = error.Message;
            }
        }
    }
}
