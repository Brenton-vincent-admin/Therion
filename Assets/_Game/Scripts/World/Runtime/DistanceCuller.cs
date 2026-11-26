using System.Collections.Generic;
using UnityEngine;

namespace Dino.World
{
    /// <summary>
    /// Hides the renderers of many small world objects (bushes, bug mounds, fish spots) beyond a distance
    /// from the camera, using Unity's CullingGroup so it costs almost nothing per frame. Gameplay
    /// components stay enabled, so hidden food still counts for AI and the "nearest food" arrow.
    /// Put it on a parent object; it manages every direct child.
    /// </summary>
    public class DistanceCuller : MonoBehaviour
    {
        [Tooltip("Children farther than this from the camera are hidden (metres). Keep it inside the fog.")]
        public float visibleDistance = 110f;
        [Tooltip("Bounding radius used for each child.")]
        public float objectRadius = 2.5f;

        CullingGroup _group;
        BoundingSphere[] _spheres;
        Renderer[][] _renderers;
        Camera _camera;

        void OnEnable()
        {
            int n = transform.childCount;
            _spheres = new BoundingSphere[n];
            _renderers = new Renderer[n][];
            var list = new List<Renderer>();
            for (int i = 0; i < n; i++)
            {
                var child = transform.GetChild(i);
                _spheres[i] = new BoundingSphere(child.position, objectRadius * Mathf.Max(1f, child.lossyScale.x));
                child.GetComponentsInChildren(true, list);
                _renderers[i] = list.ToArray();
            }
            _group = new CullingGroup();
            _group.SetBoundingSpheres(_spheres);
            _group.SetBoundingSphereCount(n);
            _group.SetBoundingDistances(new[] { visibleDistance });
            _group.onStateChanged = OnStateChanged;
            AttachCamera();
            for (int i = 0; i < n; i++) SetVisible(i, false);
        }

        void OnDisable()
        {
            if (_group != null) { _group.Dispose(); _group = null; }
            if (_renderers != null) for (int i = 0; i < _renderers.Length; i++) SetVisible(i, true);
        }

        void Update()
        {
            if (_camera == null || !_camera.isActiveAndEnabled) AttachCamera();
        }

        void AttachCamera()
        {
            _camera = Camera.main;
            if (_camera == null || _group == null) return;
            _group.targetCamera = _camera;
            _group.SetDistanceReferencePoint(_camera.transform);
            // Refresh every child's state for the new camera.
            for (int i = 0; i < _spheres.Length; i++) SetVisible(i, _group.GetDistance(i) == 0);
        }

        void OnStateChanged(CullingGroupEvent e)
        {
            if (e.currentDistance != e.previousDistance) SetVisible(e.index, e.currentDistance == 0);
        }

        void SetVisible(int index, bool visible)
        {
            var rs = _renderers[index];
            for (int i = 0; i < rs.Length; i++) if (rs[i]) rs[i].enabled = visible;
        }
    }
}
