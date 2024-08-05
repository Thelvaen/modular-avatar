using System;
using System.Collections.Generic;
using UnityEngine;

namespace nadena.dev.modular_avatar.core
{
    [Serializable]
    public struct BlendshapeBinding
    {
        public AvatarObjectReference ReferenceMesh;
        public string Blendshape;
        public string LocalBlendshape;

        public bool Equals(BlendshapeBinding other)
        {
            return Equals(ReferenceMesh, other.ReferenceMesh) && Blendshape == other.Blendshape;
        }

        public override bool Equals(object obj)
        {
            return obj is BlendshapeBinding other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((ReferenceMesh != null ? ReferenceMesh.GetHashCode() : 0) * 397) ^
                       (Blendshape != null ? Blendshape.GetHashCode() : 0);
            }
        }
    }

    [RequireComponent(typeof(SkinnedMeshRenderer))]
    [DisallowMultipleComponent]
    [ExecuteAlways]
    [AddComponentMenu("Modular Avatar/MA Blendshape Sync")]
    [HelpURL("https://modular-avatar.nadena.dev/docs/reference/blendshape-sync?lang=auto")]
    public class ModularAvatarBlendshapeSync : AvatarTagComponent
    {
        [SerializeField]
        private bool SimpleMode = false;
        [SerializeField]
        private string SourceRendererName;
        public List<BlendshapeBinding> Bindings = new List<BlendshapeBinding>();

        struct EditorBlendshapeBinding
        {
            public SkinnedMeshRenderer TargetMesh;
            public int RemoteBlendshapeIndex;
            public int LocalBlendshapeIndex;
        }

        private List<EditorBlendshapeBinding> _editorBindings = new List<EditorBlendshapeBinding>();
        private SkinnedMeshRenderer sourceRenderer = null;

        protected override void OnValidate()
        {
            base.OnValidate();

            if (RuntimeUtil.isPlaying) return;
            RuntimeUtil.delayCall(Rebind);
            RuntimeUtil.OnHierarchyChanged -= Rebind;
            RuntimeUtil.OnHierarchyChanged += Rebind;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            RuntimeUtil.OnHierarchyChanged -= Rebind;
        }

        public override void ResolveReferences()
        {
            // no-op
        }

        private void Rebind()
        {
            if (this == null) return;

            _editorBindings.Clear();
            Bindings.Clear();

            var localRenderer = GetComponent<SkinnedMeshRenderer>();
            var localMesh = localRenderer.sharedMesh;
            if (localMesh == null)
                return;

            // Simple mode
            // used to sync all blendshapes with the same name on both SkinnedMeshRenderers
            if (this.SimpleMode)
            {
                var AvatarRoot = RuntimeUtil.FindAvatarTransformInParents(this.transform);
                if (AvatarRoot == null) return;
                var renderers = AvatarRoot.gameObject.GetComponentsInChildren<SkinnedMeshRenderer>();
                if (renderers.Length == 0)
                {
                    throw new ArgumentException("No skinned mesh renderer found");
                }
                this.sourceRenderer = null;
                foreach (SkinnedMeshRenderer renderer in renderers)
                {
                    GameObject gameObject = renderer.gameObject;
                    if (gameObject.name.Equals(this.SourceRendererName))
                    {
                        this.sourceRenderer = renderer;
                        break;
                    }
                    if (this.sourceRenderer == null)
                    {
                        throw new ArgumentException("Can't find a Skinned Mesh with name " + this.SourceRendererName);
                    }
                }

                var sourceCount = this.sourceRenderer.sharedMesh.blendShapeCount;
                var targetCount = localRenderer.sharedMesh.blendShapeCount;

                // creating the AvatarObjectReference object
                // todo: rewrite this part
                var sourceReference = new AvatarObjectReference();
                sourceReference.Set(this.sourceRenderer.gameObject);
                GameObject Test = sourceReference.Get(this.sourceRenderer);
                if (Test != this.sourceRenderer.gameObject) return;

                for (int i = 0; i < sourceCount; i++)
                {
                    for (int j = 0; j < targetCount; j++)
                    {
                        if (this.sourceRenderer.sharedMesh.GetBlendShapeName(i).Equals(localRenderer.sharedMesh.GetBlendShapeName(j)))
                        {
                            _editorBindings.Add(new EditorBlendshapeBinding()
                            {
                                TargetMesh = this.sourceRenderer,
                                RemoteBlendshapeIndex = i,
                                LocalBlendshapeIndex = j
                            });
                            Bindings.Add(new BlendshapeBinding()
                            {
                                ReferenceMesh = sourceReference,
                                Blendshape = this.sourceRenderer.sharedMesh.GetBlendShapeName(i),
                                LocalBlendshape = this.sourceRenderer.sharedMesh.GetBlendShapeName(i)
                            });
                        }
                    }
                }
            }
            else
            {
                foreach (var binding in Bindings)
                {
                    var obj = binding.ReferenceMesh.Get(this);
                    if (obj == null)
                        continue;
                    var smr = obj.GetComponent<SkinnedMeshRenderer>();
                    if (smr == null)
                        continue;
                    var mesh = smr.sharedMesh;
                    if (mesh == null)
                        continue;

                    var localShape = string.IsNullOrWhiteSpace(binding.LocalBlendshape)
                        ? binding.Blendshape
                        : binding.LocalBlendshape;
                    var localIndex = localMesh.GetBlendShapeIndex(localShape);
                    var refIndex = mesh.GetBlendShapeIndex(binding.Blendshape);
                    if (localIndex == -1 || refIndex == -1)
                        continue;

                    _editorBindings.Add(new EditorBlendshapeBinding()
                    {
                        TargetMesh = smr,
                        RemoteBlendshapeIndex = refIndex,
                        LocalBlendshapeIndex = localIndex
                    });
                }
            }
            this.Update();
        }

        private void Update()
        {
            if (RuntimeUtil.isPlaying) return;

            if (_editorBindings == null) return;
            var localRenderer = GetComponent<SkinnedMeshRenderer>();
            if (localRenderer == null) return;
            foreach (var binding in _editorBindings)
            {
                if (binding.TargetMesh == null) return;
                var weight = binding.TargetMesh.GetBlendShapeWeight(binding.RemoteBlendshapeIndex);
                localRenderer.SetBlendShapeWeight(binding.LocalBlendshapeIndex, weight);
            }
        }
    }
}
