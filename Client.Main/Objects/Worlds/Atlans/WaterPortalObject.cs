using Client.Main.Content;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Threading.Tasks;
using Client.Data.BMD;
using Client.Main.Graphics;

namespace Client.Main.Objects.Worlds.Atlans
{
    public class WaterPortalObject : ModelObject
    {
        private const float TEXTURE_SCROLL_SPEED = 0.1f; // Reduced scroll speed for smoother dynamics
        private const float WAVE_FREQUENCY = 1.5f;
        private const float WAVE_AMPLITUDE = 0.2f; // Reduced amplitude for less dynamic offset
        private const float VERTEX_WAVE_HEIGHT = 0.4f;
        private const float SPATIAL_FREQUENCY = 0.8f;

        private float _currentOffset = 0f;
        private float _waveTime = 0f;
        // Removed fixed time step accumulation for texture offset
        private BMDTexCoord[] _originalTexCoords;
        private BMDTextureVertex[] _originalVertices;
        private System.Numerics.Vector3[] _vertexOffsets;

        public override async Task Load()
        {
            BlendState = BlendState.NonPremultiplied;
            LightEnabled = true;
            IsTransparent = true;
            BlendMeshState = BlendState.Additive;
            Alpha = 0.5f;
            Model = await BMDLoader.Instance.Prepare($"Object8/Object24.bmd");

            await base.Load();

            if (Model?.Meshes != null && Model.Meshes.Length > 0)
            {
                var mesh = Model.Meshes[0];
                _originalTexCoords = new BMDTexCoord[mesh.TexCoords.Length];
                Array.Copy(mesh.TexCoords, _originalTexCoords, mesh.TexCoords.Length);

                _originalVertices = new BMDTextureVertex[mesh.Vertices.Length];
                Array.Copy(mesh.Vertices, _originalVertices, mesh.Vertices.Length);

                _vertexOffsets = new System.Numerics.Vector3[mesh.Vertices.Length];
            }

            // Set a slight blue tint for the texture
            this.Color = new Color(200, 220, 255);
        }

        private float CalculateWaveOffset(float x, float z, float time)
        {
            float wave1 = (float)Math.Sin(time * WAVE_FREQUENCY + (x + z) * SPATIAL_FREQUENCY);
            float wave2 = (float)Math.Sin(time * WAVE_FREQUENCY * 0.5f + (x - z) * SPATIAL_FREQUENCY * 1.3f) * 0.5f;
            float wave3 = (float)Math.Cos(time * WAVE_FREQUENCY * 0.7f + z * SPATIAL_FREQUENCY * 0.8f) * 0.3f;
            return (wave1 + wave2 + wave3) * 0.33f;
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (Model?.Meshes == null || Model.Meshes.Length == 0 || _originalTexCoords == null)
                return;

            // PERFORMANCE: Skip intensive CPU vertex animation if out of view or too far
            var camDistSq = Vector3.DistanceSquared(Camera.Instance.Position, WorldPosition.Translation);
            const float MaxAnimDistanceSq = 3000f * 3000f; // Skip animation if over 30 tiles away
            if (camDistSq > MaxAnimDistanceSq || !Camera.Instance.Frustum.Intersects(BoundingBoxWorld))
                return;

            // Update wave time uniformly
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            _waveTime += dt * 0.5f;

            // Update texture scrolling offset uniformly based on delta time
            _currentOffset = (_currentOffset + TEXTURE_SCROLL_SPEED * dt) % 1.0f;

            var mesh = Model.Meshes[0];

            // Update vertex offsets based on wave effect
            for (int i = 0; i < mesh.Vertices.Length; i++)
            {
                var originalVertex = _originalVertices[i];
                float waveOffset = CalculateWaveOffset(
                    originalVertex.Position.X,
                    originalVertex.Position.Z,
                    _waveTime
                );
                _vertexOffsets[i] = new System.Numerics.Vector3(0, waveOffset * VERTEX_WAVE_HEIGHT, 0);
            }

            // Update vertex positions and UVs in a single pass over triangles to minimize overhead
            foreach (var triangle in mesh.Triangles)
            {
                for (int i = 0; i < triangle.VertexIndex.Length; i++)
                {
                    int vertexIdx = triangle.VertexIndex[i];
                    int texCoordIdx = triangle.TexCoordIndex[i];

                    if (vertexIdx >= 0 && vertexIdx < mesh.Vertices.Length)
                    {
                        // Apply wave offset to position
                        var vertex = _originalVertices[vertexIdx];
                        vertex.Position += _vertexOffsets[vertexIdx];
                        mesh.Vertices[vertexIdx] = vertex;

                        // Apply scrolling and wave-based UV offset
                        if (texCoordIdx >= 0 && texCoordIdx < mesh.TexCoords.Length)
                        {
                            var texCoord = _originalTexCoords[texCoordIdx];
                            float uvOffset = Math.Abs(_vertexOffsets[vertexIdx].Y / VERTEX_WAVE_HEIGHT) * WAVE_AMPLITUDE;
                            texCoord.V += _currentOffset + uvOffset;
                            mesh.TexCoords[texCoordIdx] = texCoord;
                        }
                    }
                }
            }

            InvalidateBuffers();
        }

        public override void Draw(GameTime gameTime)
        {
            // Set the sampler state to LinearWrap to ensure the texture loops correctly
            GraphicsDevice.SamplerStates[0] = SamplerState.LinearWrap;

            // Adjust alpha based on wave time for additional water effect
            Alpha = 0.4f + (float)Math.Abs(Math.Sin(_waveTime * WAVE_FREQUENCY * 0.3f)) * 0.2f;
            base.Draw(gameTime);
        }
    }
}
