using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Landoria.ModelForge
{
    // Renders a static model thumbnail with a temporary camera and lights.
    internal static class PreviewCapture
    {
        // Saves a square JPEG beside an exported GLB.
        internal static void Write(GameObject source, string path)
        {
            var objects = new List<GameObject>();
            var target = RenderTexture.GetTemporary(768, 768, 24, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            var image = new Texture2D(768, 768, TextureFormat.RGB24, false);
            try
            {
                var bounds = CopyVisuals(source, objects);
                var camera = new GameObject("ModelForge thumbnail camera");
                objects.Add(camera);
                ConfigureCamera(camera.AddComponent<Camera>(), bounds, target);
                AddLight(objects, bounds.center + new Vector3(-4, 6, -3), bounds.center, 1.4f);
                AddLight(objects, bounds.center + new Vector3(4, 2, 4), bounds.center, .7f);
                camera.GetComponent<Camera>().Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 768, 768), 0, 0);
                image.Apply();
                File.WriteAllBytes(path, image.EncodeToJPG(88));
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                UnityEngine.Object.Destroy(image);
                foreach (var item in objects) { UnityEngine.Object.Destroy(item); }
            }
        }

        // Copies only mesh and material references into isolated render objects.
        private static Bounds CopyVisuals(GameObject source, List<GameObject> objects)
        {
            var bounds = new Bounds(Vector3.zero, Vector3.zero);
            var first = true;
            foreach (var filter in source.GetComponentsInChildren<MeshFilter>(true))
            {
                var renderer = filter.GetComponent<MeshRenderer>();
                if (renderer == null || filter.sharedMesh == null || !renderer.enabled || !IsFirstLod(renderer)) { continue; }
                var copy = new GameObject("ModelForge thumbnail mesh");
                objects.Add(copy);
                copy.layer = 30;
                copy.transform.position = source.transform.InverseTransformPoint(filter.transform.position);
                copy.transform.rotation = Quaternion.Inverse(source.transform.rotation) * filter.transform.rotation;
                copy.transform.localScale = filter.transform.lossyScale;
                copy.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                copy.AddComponent<MeshRenderer>().sharedMaterials = renderer.sharedMaterials;
                var box = copy.GetComponent<MeshRenderer>().bounds;
                if (first) { bounds = box; first = false; }
                else { bounds.Encapsulate(box); }
            }
            if (first) { throw new InvalidOperationException("No mesh for JPEG preview."); }
            return bounds;
        }

        // Keeps only the most detailed renderer from each LOD group.
        private static bool IsFirstLod(MeshRenderer renderer)
        {
            var group = renderer.GetComponentInParent<LODGroup>();
            if (group == null) { return true; }
            var levels = group.GetLODs();
            if (levels.Length == 0) { return true; }
            foreach (var item in levels[0].renderers)
            {
                if (item == renderer) { return true; }
            }
            foreach (var level in levels)
            {
                foreach (var item in level.renderers)
                {
                    if (item == renderer) { return false; }
                }
            }
            return true;
        }

        // Frames the copied mesh against a neutral background.
        private static void ConfigureCamera(Camera camera, Bounds bounds, RenderTexture target)
        {
            var size = Mathf.Max(bounds.extents.magnitude, .5f);
            camera.transform.position = bounds.center + new Vector3(size * 1.8f, size * 1.2f, size * 1.8f);
            camera.transform.LookAt(bounds.center);
            camera.orthographic = true;
            camera.orthographicSize = size * 1.45f;
            camera.nearClipPlane = .01f;
            camera.farClipPlane = size * 10f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.22f, .24f, .27f);
            camera.cullingMask = 1 << 30;
            camera.targetTexture = target;
            camera.enabled = false;
        }

        // Adds a light used only for the thumbnail render.
        private static void AddLight(List<GameObject> objects, Vector3 position, Vector3 center, float strength)
        {
            var holder = new GameObject("ModelForge thumbnail light");
            objects.Add(holder);
            holder.transform.position = position;
            holder.transform.LookAt(center);
            var light = holder.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = strength;
            light.cullingMask = 1 << 30;
        }
    }
}
