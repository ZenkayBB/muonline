#nullable enable
using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Client.Main.Objects.Effects
{
    /// <summary>
    /// Simple trail effect for Evil Spirit projectiles.
    /// Renders a ribbon of quads that follow a position over time.
    /// </summary>
    public sealed class EvilSpiritTrail : IDisposable
    {
        // Configuration
        private const int MaxPoints = 150;
        private const float MinDistance = 1.0f; 
        private const float TrailWidth = 70f;
        private const float FadeDuration = 0.8f;

        // Trail color - solid black for evil spirits
        private static readonly Color TrailColor = new Color(0, 0, 0, 100);

        // Trail points
        private readonly Vector3[] _positions = new Vector3[MaxPoints];
        private readonly float[] _times = new float[MaxPoints];
        private int _pointCount;
        private Vector3 _lastPosition;
        private bool _initialized;

        // Graphics
        private readonly VertexPositionColor[] _vertices;
        private readonly short[] _indices;
        private BasicEffect? _effect;
        private GraphicsDevice? _device;
        private bool _disposed;

        public float Alpha { get; set; } = 1f;

        public EvilSpiritTrail()
        {
            // Pre-allocate buffers for a continuous ribbon
            // Total points N -> N segments -> 2*N vertices
            _vertices = new VertexPositionColor[MaxPoints * 2];
            // Total segments N-1 -> Each segment 2 triangles (6 indices)
            _indices = new short[(MaxPoints - 1) * 6];

            // Pre-build indices for the ribbon
            for (int i = 0; i < MaxPoints - 1; i++)
            {
                int vi = i * 2;
                int ii = i * 6;
                // Segment between point i and i+1
                _indices[ii + 0] = (short)(vi + 0);
                _indices[ii + 1] = (short)(vi + 1);
                _indices[ii + 2] = (short)(vi + 2);
                _indices[ii + 3] = (short)(vi + 1);
                _indices[ii + 4] = (short)(vi + 3);
                _indices[ii + 5] = (short)(vi + 2);
            }
        }

        public void Initialize(GraphicsDevice device)
        {
            if (_device != null) return;
            _device = device;
            _effect = new BasicEffect(device)
            {
                VertexColorEnabled = true,
                TextureEnabled = false,
                LightingEnabled = false
            };
        }

        public void AddPoint(Vector3 position, float totalTime)
        {
            if (!_initialized)
            {
                _lastPosition = position;
                _initialized = true;
            }

            float dist = Vector3.Distance(position, _lastPosition);
            if (dist < MinDistance && _pointCount > 0)
                return;

            // Shift points (0 is the newest)
            int count = Math.Min(_pointCount, MaxPoints - 1);
            for (int i = count; i > 0; i--)
            {
                _positions[i] = _positions[i - 1];
                _times[i] = _times[i - 1];
            }

            _positions[0] = position;
            _times[0] = totalTime;
            
            if (_pointCount < MaxPoints)
                _pointCount++;

            _lastPosition = position;
        }

        public void Draw(Matrix view, Matrix projection, float currentTime)
        {
            if (_effect == null || _device == null || _pointCount < 2)
                return;

            // 1. Calculate side offsets for each point for a smooth transition at joints
            // Total points N -> Total vertices 2*N
            int activePoints = 0;
            byte baseAlpha = (byte)(TrailColor.A * Alpha);
            Color finalColor = new Color(TrailColor.R, TrailColor.G, TrailColor.B, baseAlpha);

            for (int i = 0; i < _pointCount; i++)
            {
                float age = currentTime - _times[i];
                if (age > FadeDuration && i > 1) break; // Stop processing old points

                Vector3 direction;
                if (i == 0) // Front point
                    direction = Vector3.Normalize(_positions[0] - _positions[1]);
                else if (i == _pointCount - 1) // End point
                    direction = Vector3.Normalize(_positions[i-1] - _positions[i]);
                else // Joint (Average of segments)
                    direction = Vector3.Normalize(_positions[i-1] - _positions[i+1]);

                Vector3 side = Vector3.Normalize(Vector3.Cross(direction, Vector3.UnitZ));
                if (side.LengthSquared() < 0.001f)
                    side = Vector3.Normalize(Vector3.Cross(direction, Vector3.UnitY));

                // Width tapering at the start (first 10 segments)
                float tapering = 1f;
                const int taperingPoints = 10;
                if (i < taperingPoints)
                {
                    tapering = i / (float)taperingPoints;
                }

                Vector3 offset = side * (TrailWidth * 0.5f * tapering);
                
                int vi = i * 2;
                _vertices[vi + 0] = new VertexPositionColor(_positions[i] - offset, finalColor);
                _vertices[vi + 1] = new VertexPositionColor(_positions[i] + offset, finalColor);
                
                activePoints++;
            }

            if (activePoints < 2) return;

            // Save states
            var prevBlend = _device.BlendState;
            var prevDepth = _device.DepthStencilState;
            var prevRaster = _device.RasterizerState;

            _device.BlendState = BlendState.AlphaBlend;
            _device.DepthStencilState = DepthStencilState.DepthRead;
            _device.RasterizerState = RasterizerState.CullNone;

            _effect.View = view;
            _effect.Projection = projection;
            _effect.World = Matrix.Identity;

            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                // Draw triangles for all active segments
                _device.DrawUserIndexedPrimitives(
                    PrimitiveType.TriangleList,
                    _vertices, 0, activePoints * 2,
                    _indices, 0, (activePoints - 1) * 2);
            }

            // Restore states
            _device.BlendState = prevBlend;
            _device.DepthStencilState = prevDepth;
            _device.RasterizerState = prevRaster;
        }

        public void Clear()
        {
            _pointCount = 0;
            _initialized = false;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _effect?.Dispose();
            _effect = null;
        }
    }
}

