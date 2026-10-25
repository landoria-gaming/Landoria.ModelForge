using System;
using System.Collections.Generic;
using System.IO;
using BepInEx.Logging;
using UnityEngine;

namespace Landoria.ModelForge
{
    // Tracks temporary imported models and a pending local placement.
    internal sealed class ForgeSession : IDisposable
    {
        private readonly ManualLogSource _log;
        private readonly List<GameObject> _placed = new List<GameObject>();
        private GameObject _preview;
        private float _angle;
        internal string Folder { get; }

        // Stores the local working folder and logger.
        internal ForgeSession(string folder, ManualLogSource log)
        {
            Folder = folder;
            _log = log;
        }

        // Exports the static visual hierarchy under the cursor.
        internal string ExportTarget()
        {
            if (!TryHit(out var hit))
            {
                throw new InvalidOperationException("Aim at an object first.");
            }
            var view = hit.collider.GetComponentInParent<ZNetView>();
            var root = view != null ? view.gameObject : hit.collider.gameObject;
            var name = SafeName(root.name);
            var path = UniquePath(name);
            GlbWriter.Write(root, path);
            try
            {
                PreviewCapture.Write(root, Path.ChangeExtension(path, ".jpg"));
            }
            catch (Exception error)
            {
                _log.LogWarning("JPEG preview failed: " + error.Message);
                return "Exported " + Path.GetFileName(path) + "; JPEG failed.";
            }
            return "Exported " + Path.GetFileName(path) + " and JPEG.";
        }

        // Loads an imported model as a movable local preview.
        internal string BeginPlacement(string path)
        {
            CancelPlacement();
            _preview = GlbReader.Read(path);
            _preview.name = "ModelForge: " + Path.GetFileNameWithoutExtension(path);
            _angle = 0;
            return "Aim, use wheel to rotate, click to place, Esc to cancel.";
        }

        // Moves and confirms the pending model without saving it to the world.
        internal void UpdatePlacement()
        {
            if (_preview == null)
            {
                return;
            }
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                CancelPlacement();
                return;
            }
            if (TryHit(out var hit))
            {
                _preview.transform.position = hit.point;
            }
            _angle += Input.mouseScrollDelta.y * 15f;
            _preview.transform.rotation = Quaternion.Euler(0, _angle, 0);
            if (Input.GetMouseButtonDown(0) && TryHit(out _))
            {
                var collider = _preview.AddComponent<BoxCollider>();
                collider.center = LocalBounds(_preview).center;
                collider.size = LocalBounds(_preview).size;
                _placed.Add(_preview);
                _preview = null;
            }
        }

        // Deletes only a previously placed ModelForge object.
        internal string EraseTarget()
        {
            if (!TryHit(out var hit))
            {
                return "No imported model under cursor.";
            }
            var target = hit.collider.gameObject;
            if (!_placed.Remove(target))
            {
                return "No imported model under cursor.";
            }
            UnityEngine.Object.Destroy(target);
            return "Imported model erased.";
        }

        // Casts from the center of the game camera.
        private static bool TryHit(out RaycastHit hit)
        {
            var camera = Camera.main;
            hit = default;
            return camera != null && Physics.Raycast(camera.ViewportPointToRay(new Vector3(.5f, .5f)), out hit, 100f);
        }

        // Computes a bounding box in the imported root's local space.
        private static Bounds LocalBounds(GameObject root)
        {
            var bounds = new Bounds(Vector3.zero, Vector3.zero);
            var first = true;
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>())
            {
                var mesh = filter.sharedMesh;
                if (mesh == null)
                {
                    continue;
                }
                foreach (var vertex in mesh.vertices)
                {
                    var point = root.transform.InverseTransformPoint(filter.transform.TransformPoint(vertex));
                    if (first) { bounds = new Bounds(point, Vector3.zero); first = false; }
                    else { bounds.Encapsulate(point); }
                }
            }
            return bounds;
        }

        // Removes characters unsuitable for a file name.
        private static string SafeName(string name)
        {
            foreach (var character in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(character, '_');
            }
            return string.IsNullOrWhiteSpace(name) ? "Model" : name.Replace("(Clone)", "").Trim();
        }

        // Keeps earlier exports when an object is exported again.
        private string UniquePath(string name)
        {
            var path = Path.Combine(Folder, name + ".glb");
            for (var number = 2; File.Exists(path); number++)
            {
                path = Path.Combine(Folder, name + "_" + number + ".glb");
            }
            return path;
        }

        // Discards the current placement preview.
        private void CancelPlacement()
        {
            if (_preview != null)
            {
                UnityEngine.Object.Destroy(_preview);
                _preview = null;
            }
        }

        // Clears all temporary models on shutdown.
        public void Dispose()
        {
            CancelPlacement();
            foreach (var model in _placed)
            {
                if (model != null) { UnityEngine.Object.Destroy(model); }
            }
            _placed.Clear();
        }
    }
}
