using Client.Main.Content;
using Client.Main.Controls;
using Client.Main.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Threading.Tasks;


namespace Client.Main.Objects.Effects
{
    /// <summary>
    /// Model projectile that launches Laser01 meshes outward with a simple trail.
    /// </summary>
    public sealed class EvilSpiritLaserProjectile : ModelObject
    {
        private readonly WalkerObject _caster;
        private readonly Vector3 _origin;
        private readonly string _modelPath;
        private readonly Vector3 _direction;
        private readonly float _speed;
        private readonly float _spawnZ;
        private readonly float _lifetime;
        private readonly float _initialPhase;
        private readonly EvilSpiritTrail _trail;
        private float _elapsed;
        private float _totalTime;
        private bool _disposed;
        private Vector3 _lastForward = Vector3.UnitX;

        public EvilSpiritLaserProjectile(
            WalkerObject caster,
            string modelPath,
            Vector3 direction,
            float speed,
            float lifetimeSeconds,
            float heightOffset,
            int projectileIndex,
            int projectileCount,
            int seed,
            Action<EvilSpiritLaserProjectile>? onCompleted = null)
        {
            _caster = caster ?? throw new ArgumentNullException(nameof(caster));
            if (string.IsNullOrWhiteSpace(modelPath))
                throw new ArgumentException("Model path cannot be empty.", nameof(modelPath));

            _modelPath = modelPath.Replace('\\', '/');
            _direction = direction.LengthSquared() < 0.0001f ? Vector3.UnitX : Vector3.Normalize(direction);
            _speed = MathF.Max(250f, speed);
            _lifetime = MathF.Max(0.8f, lifetimeSeconds);

            var origin = _caster.Position;
            _spawnZ = origin.Z + heightOffset;
            Position = new Vector3(origin.X, origin.Y, _spawnZ);
            _origin = new Vector3(origin.X, origin.Y, _spawnZ);
            Angle = Vector3.Zero;
            Scale = 0.95f;
            Alpha = 1f;
            BlendState = BlendState.NonPremultiplied;
            BlendMeshState = BlendState.NonPremultiplied;
            BlendMesh = -1;
            RenderShadow = false;
            LightEnabled = false;
            UseSunLight = false;
            GlowIntensity = 0f;
            SimpleColorMode = true;
            EnableCustomShader = true;
            Color = Color.Black;
            BoundingBoxLocal = new BoundingBox(new Vector3(-5f, -5f, -5f), new Vector3(5f, 5f, 5f));

            // Create simple trail
            _trail = new EvilSpiritTrail();

            // small phase jitter to avoid perfectly symmetric streams
            _initialPhase = (projectileIndex / MathF.Max(1, projectileCount)) * MathHelper.TwoPi + (seed & 0xFFFF) * 1e-4f;
        }

        public override async Task LoadContent()
        {
            Model = await BMDLoader.Instance.Prepare(_modelPath);

            // Initialize trail graphics
            if (GraphicsDevice != null)
            {
                _trail.Initialize(GraphicsDevice);
            }

            await base.LoadContent();
        }

        public override void Update(GameTime gameTime)
        {
            if (_disposed || _caster.World != World)
            {
                Complete();
                return;
            }

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (dt <= 0f)
            {
                base.Update(gameTime);
                return;
            }

            _elapsed += dt;
            _totalTime += dt;

            if (_elapsed >= _lifetime)
            {
                Complete();
                return;
            }

            float progress = MathHelper.Clamp(_elapsed / _lifetime, 0f, 1f);

            // Fade out model and trail as lifetime progresses
            float fade = 1f - MathHelper.SmoothStep(0f, 1f, progress);
            Alpha = fade;
            _trail.Alpha = fade;

            // Forward movement with small circular motion around origin for visual complexity
            float angle = _initialPhase + (0.5f * _elapsed);
            float radius = 8f * (1f - progress);
            Vector3 offset = new Vector3(MathF.Cos(angle) * radius, MathF.Sin(angle) * radius, 0f);
            Vector3 next = _origin + offset + (_direction * _speed * _elapsed);
            Vector3 displacement = next - Position;
            Position = next;
            if (displacement.LengthSquared() > 1e-6f)
                _lastForward = Vector3.Normalize(displacement);

            var f = _lastForward;
            float yaw = MathF.Atan2(f.Y, f.X) + MathHelper.Pi;
            const float modelYawOffset = -MathHelper.PiOver2;
            Angle = new Vector3(0f, 0f, yaw + modelYawOffset);

            // Add point to trail
            _trail.AddPoint(Position, _totalTime);

            base.Update(gameTime);
        }

        public override void Draw(GameTime gameTime)
        {
            base.Draw(gameTime);

            // Draw trail
            if (!_disposed && Camera.Instance != null && GraphicsDevice != null)
            {
                _trail.Draw(Camera.Instance.View, Camera.Instance.Projection, _totalTime);
            }
        }

        private void Complete()
        {
            if (_disposed)
                return;

            _disposed = true;
            _trail.Dispose();
            World?.RemoveObject(this);
            Dispose();
        }

        public override void Dispose()
        {
            _disposed = true;
            _trail?.Dispose();
            base.Dispose();
        }
    }
}

