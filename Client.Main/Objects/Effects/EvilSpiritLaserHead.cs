#nullable enable
using System;
using System.Threading.Tasks;
using Client.Main.Content;
using Client.Main.Controls;
using Client.Main.Graphics;
using Client.Main.Objects;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;


namespace Client.Main.Objects.Effects
{
    public sealed class EvilSpiritLaserHead : ModelObject
    {
        private const string LaserModelPath = "Skill/Laser01.bmd";

        private readonly EvilSpiritTrail _trail;
        private float _totalTime;

        public EvilSpiritLaserHead()
        {
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

            // Create trail
            _trail = new EvilSpiritTrail();
        }

        public override async Task LoadContent()
        {
            Model = await BMDLoader.Instance.Prepare(LaserModelPath);

            // Initialize trail
            if (GraphicsDevice != null)
            {
                _trail.Initialize(GraphicsDevice);
            }

            await base.LoadContent();
        }

        public override void Update(GameTime gameTime)
        {
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            _totalTime += dt;

            // Add trail point
            _trail.AddPoint(Position, _totalTime);
            _trail.Alpha = Alpha;

            base.Update(gameTime);
        }

        public override void Draw(GameTime gameTime)
        {
            base.Draw(gameTime);

            // Draw trail
            if (Camera.Instance != null && GraphicsDevice != null)
            {
                _trail.Draw(Camera.Instance.View, Camera.Instance.Projection, _totalTime);
            }
        }

        public void UpdateTransform(Vector3 position, Vector3 angle)
        {
            Position = position;
            
            // Convert degrees to radians for ModelObject
            // NOTE: previous +270º caused the head to face opposite; +90º flips it to the correct direction
            Angle = new Vector3(
                MathHelper.ToRadians(angle.X),
                MathHelper.ToRadians(angle.Y),
                MathHelper.ToRadians(angle.Z + 90f) // Adjusted offset to correct facing
            );
        }

        public override void Dispose()
        {
            _trail?.Dispose();
            base.Dispose();
        }
    }
}

