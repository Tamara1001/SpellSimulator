using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SpellSimulator.EditorTools
{
    /// <summary>
    /// Genera por codigo las decoraciones de la interfaz (Assets/_Project/UI/Sprites): marcos de pergamino y de tinta con
    /// filigrana dorada (9-slice), keycap, gota de tinta, pluma, sello de cera, iconos y hojas del libro. Se dibujan con
    /// funciones de distancia con antialias y se importan como Sprite con su borde de 9-slice. Deterministas: regenerar da
    /// el mismo PNG. Lo llama <see cref="UISetup.Apply"/>.
    /// </summary>
    public static class UISpriteGenerator
    {
        public const string Folder = "Assets/_Project/UI/Sprites";

        private static readonly Color Gold = C(222, 172, 78);
        private static readonly Color GoldLight = C(250, 226, 156);
        private static readonly Color GoldDark = C(140, 96, 38);
        private static readonly Color Bronze = C(122, 92, 52);
        private static readonly Color VioletDeep = C(30, 18, 42, 0.88f);
        private static readonly Color Violet = C(84, 50, 116);
        private static readonly Color Cream = C(247, 236, 210);
        private static readonly Color Parchment = C(238, 222, 186);
        private static readonly Color InkBrown = C(70, 46, 28);

        public static void GenerateAll()
        {
            Directory.CreateDirectory(Folder);
            Save("panel_dark", PanelDark(), 16);
            Save("tray", Tray(), 18);
            Save("parchment", ParchmentCard(false), 18);
            Save("parchment_warn", ParchmentCard(true), 18);
            Save("slot", Slot(false), 14);
            Save("slot_active", Slot(true), 14);
            Save("glow", Glow(), 0);
            Save("keycap", Keycap(), 8);
            Save("drop", Drop(), 0);
            Save("quill", Quill(), 0);
            Save("warn", Warn(), 0);
            Save("star", Star(), 0);
            Save("gem", Gem(), 0);
            Save("empty", Empty(), 0);
            Save("divider", Divider(), 0);
            Save("page", Page(), 24);
            Save("gutter", Gutter(), 0);
            Save("cover", Cover(), 28);
            Save("ribbon", Ribbon(), 0);
            Save("seal", Seal(), 0);
            Save("button", ButtonFrame(), 16);
            Save("tab", Tab(), 12);
            AssetDatabase.Refresh();
        }

        // ---------------------------------------------------------------- piezas

        private static Img PanelDark()
        {
            var img = new Img(64, 64);
            Vector2 c = img.Center; Vector2 h = new Vector2(30f, 30f);
            img.Fill(p => Sd.RoundRect(p, c, h, 10f), VioletDeep);
            img.Stroke(p => Sd.RoundRect(p, c, h, 10f), 1.8f, Gold.A(0.85f));
            img.Stroke(p => Sd.RoundRect(p, c, h - new Vector2(4f, 4f), 7f), 0.9f, Gold.A(0.28f));
            Corners(img, 7f, 3.2f, GoldLight);
            return img;
        }

        private static Img Tray()
        {
            var img = new Img(72, 72);
            Vector2 c = img.Center; Vector2 h = new Vector2(34f, 34f);
            img.FillGradient(p => Sd.RoundRect(p, c, h, 14f), C(40, 24, 54, 0.78f), C(18, 10, 26, 0.9f));
            img.Stroke(p => Sd.RoundRect(p, c, h, 14f), 1.6f, Bronze.A(0.95f));
            img.Stroke(p => Sd.RoundRect(p, c, h - new Vector2(4f, 4f), 11f), 0.8f, Gold.A(0.3f));
            Corners(img, 8f, 3.4f, Gold);
            return img;
        }

        private static Img ParchmentCard(bool warn)
        {
            var img = new Img(64, 64);
            Vector2 c = img.Center; Vector2 h = new Vector2(29f, 29f);
            img.FillParchment(p => Sd.RoundRect(p, c, h, 6f), Cream, Parchment, 0.07f);
            img.Stroke(p => Sd.RoundRect(p, c, h, 6f), warn ? 2.4f : 1.4f, warn ? C(214, 130, 40) : GoldDark.A(0.85f));
            img.Stroke(p => Sd.RoundRect(p, c, h - new Vector2(4f, 4f), 3f), 0.7f, (warn ? C(214, 130, 40) : GoldDark).A(0.35f));
            Corners(img, 6f, 2.6f, warn ? C(232, 150, 52) : Gold);
            // Sombra suave bajo la tarjeta.
            img.Under(p => Sd.RoundRect(p - new Vector2(0f, 1.5f), c, h, 6f), 3f, C(0, 0, 0, 0.35f));
            return img;
        }

        private static Img Slot(bool active)
        {
            var img = new Img(64, 64);
            Vector2 c = img.Center; Vector2 h = new Vector2(30f, 30f);
            img.FillGradient(p => Sd.RoundRect(p, c, h, 9f), active ? C(74, 44, 98, 0.92f) : C(46, 30, 60, 0.82f), active ? C(36, 20, 50, 0.95f) : C(22, 14, 30, 0.88f));
            // Sombra interior arriba.
            img.Stroke(p => Sd.RoundRect(p - new Vector2(0f, -1.5f), c, h - new Vector2(2f, 2f), 8f), 2.5f, C(0, 0, 0, 0.25f));
            img.Stroke(p => Sd.RoundRect(p, c, h, 9f), active ? 2.6f : 1.6f, active ? GoldLight : Bronze);
            if (active)
            {
                img.Stroke(p => Sd.RoundRect(p, c, h - new Vector2(4f, 4f), 6f), 1.2f, Gold.A(0.55f));
                Corners(img, 6.5f, 3.4f, GoldLight);
            }
            else img.Stroke(p => Sd.RoundRect(p, c, h - new Vector2(3.5f, 3.5f), 6f), 0.7f, Gold.A(0.18f));
            return img;
        }

        private static Img Glow()
        {
            var img = new Img(128, 128);
            Vector2 c = img.Center;
            img.Paint(p =>
            {
                float d = Sd.RoundRect(p, c, new Vector2(34f, 34f), 12f);
                float a = Mathf.Exp(-Mathf.Max(0f, d) / 9f) * (d < 0f ? 0.35f : 1f);
                return GoldLight.A(0.75f * a);
            });
            return img;
        }

        private static Img Keycap()
        {
            var img = new Img(32, 32);
            Vector2 c = img.Center;
            // Base (canto inferior oscuro) y cara superior clara: tecla en relieve.
            img.Fill(p => Sd.RoundRect(p, c, new Vector2(15f, 15f), 5f), C(150, 118, 78));
            img.FillGradient(p => Sd.RoundRect(p, c + new Vector2(0f, 1.6f), new Vector2(13.6f, 13.4f), 4.2f), C(252, 244, 222), C(226, 208, 170));
            img.Stroke(p => Sd.RoundRect(p, c, new Vector2(15f, 15f), 5f), 1f, C(70, 48, 28, 0.9f));
            return img;
        }

        private static Img Drop()
        {
            var img = new Img(32, 40);
            Vector2 c = new Vector2(16f, 15f);
            Func<Vector2, float> drop = p => Sd.Drop(p, c, 10.5f, 24f);
            img.FillGradient(drop, C(170, 120, 230), C(76, 36, 120));
            img.Stroke(drop, 1.2f, C(30, 14, 44, 0.9f));
            img.Fill(p => Sd.Circle(p, c + new Vector2(-4f, 3f), 2.4f), C(255, 255, 255, 0.75f));
            return img;
        }

        private static Img Quill()
        {
            var img = new Img(40, 40);
            Vector2 a = new Vector2(8f, 6f), b = new Vector2(33f, 35f);
            Vector2 dir = (b - a).normalized; Vector2 n = new Vector2(-dir.y, dir.x);
            // Pluma: hoja ahusada a lo largo del canon.
            img.Fill(p =>
            {
                float t = Mathf.Clamp01(Vector2.Dot(p - a, dir) / Vector2.Distance(a, b));
                float width = Mathf.Sin(Mathf.Clamp01((t - 0.18f) / 0.82f) * Mathf.PI) * 7.5f * (t > 0.18f ? 1f : 0f);
                float side = Vector2.Dot(p - a, n);
                float lateral = Mathf.Abs(side + width * 0.25f) - width * 0.6f;
                float along = Mathf.Max(-Vector2.Dot(p - a, dir), Vector2.Dot(p - b, dir));
                return Mathf.Max(lateral, along);
            }, C(126, 84, 160));
            img.Stroke(p => Sd.Segment(p, a, b), 1.5f, C(60, 36, 20));
            img.Fill(p => Sd.Segment(p, a, a + dir * 5f) - 1.2f, C(40, 24, 14));
            return img;
        }

        private static Img Warn()
        {
            var img = new Img(40, 40);
            Vector2 c = img.Center;
            img.FillGradient(p => Sd.Circle(p, c, 17f), C(246, 176, 72), C(206, 112, 32));
            img.Stroke(p => Sd.Circle(p, c, 17f), 1.4f, C(110, 50, 16));
            img.Fill(p => Sd.Segment(p, c + new Vector2(0f, 9f), c + new Vector2(0f, -2f)) - 2.2f, C(70, 28, 10));
            img.Fill(p => Sd.Circle(p, c + new Vector2(0f, -8f), 2.4f), C(70, 28, 10));
            return img;
        }

        private static Img Star()
        {
            var img = new Img(40, 40);
            Vector2 c = img.Center;
            img.FillGradient(p => Sd.Star4(p, c, 18f, 4.2f), GoldLight, C(196, 132, 40));
            img.Stroke(p => Sd.Star4(p, c, 18f, 4.2f), 1f, GoldDark);
            img.Fill(p => Sd.Star4(p, c + new Vector2(10f, 10f), 6f, 1.6f), Gold.A(0.9f));
            return img;
        }

        private static Img Gem()
        {
            var img = new Img(40, 40);
            Vector2 c = img.Center;
            // Blanca para teñirse con el color del objeto (unity-background-image-tint-color).
            img.FillGradient(p => Sd.Diamond(p, c, 16f), Color.white, C(170, 170, 170));
            img.Stroke(p => Sd.Diamond(p, c, 16f), 1.4f, C(60, 60, 60));
            img.Fill(p => Sd.Diamond(p, c + new Vector2(-4f, 4f), 4f), C(255, 255, 255, 0.8f));
            return img;
        }

        private static Img Empty()
        {
            var img = new Img(40, 40);
            Vector2 c = img.Center;
            img.Stroke(p => Sd.Diamond(p, c, 14f), 1.4f, Cream);
            img.Fill(p => Sd.Circle(p, c, 2.5f), Cream);
            return img;
        }

        private static Img Divider()
        {
            var img = new Img(320, 20);
            Vector2 c = img.Center;
            img.Paint(p =>
            {
                float fade = 1f - Mathf.Pow(Mathf.Abs(p.x - c.x) / 160f, 2f);
                float line = Mathf.Clamp01(1.2f - Mathf.Abs(p.y - c.y) * 1.3f);
                return GoldDark.A(Mathf.Clamp01(fade) * line * 0.9f);
            });
            img.Fill(p => Sd.Diamond(p, c, 7f), Gold);
            img.Stroke(p => Sd.Diamond(p, c, 7f), 1f, GoldDark);
            img.Fill(p => Sd.Diamond(p, c + new Vector2(-16f, 0f), 3f), GoldDark);
            img.Fill(p => Sd.Diamond(p, c + new Vector2(16f, 0f), 3f), GoldDark);
            return img;
        }

        private static Img Page()
        {
            var img = new Img(128, 128);
            Vector2 c = img.Center; Vector2 h = new Vector2(63f, 63f);
            img.FillParchment(p => Sd.RoundRect(p, c, h, 3f), C(250, 242, 222), C(240, 226, 194), 0.04f);
            // Bordes tostados.
            img.Paint(p =>
            {
                float d = -Sd.RoundRect(p, c, h, 3f);
                return C(170, 120, 60).A(Mathf.Clamp01(1f - d / 14f) * 0.28f * (d >= 0f ? 1f : 0f));
            }, true);
            img.Stroke(p => Sd.RoundRect(p, c, h - new Vector2(10f, 10f), 2f), 0.8f, GoldDark.A(0.35f));
            return img;
        }

        private static Img Gutter()
        {
            var img = new Img(64, 8);
            img.Paint(p => C(80, 50, 20).A(Mathf.Pow(p.x / 64f, 2.2f) * 0.38f));
            return img;
        }

        private static Img Cover()
        {
            var img = new Img(96, 96);
            Vector2 c = img.Center; Vector2 h = new Vector2(46f, 46f);
            img.FillParchment(p => Sd.RoundRect(p, c, h, 12f), C(76, 42, 104), C(52, 26, 72), 0.1f);
            img.Stroke(p => Sd.RoundRect(p, c, h, 12f), 2f, C(24, 12, 30));
            img.Stroke(p => Sd.RoundRect(p, c, h - new Vector2(7f, 7f), 7f), 1.6f, Gold);
            img.Stroke(p => Sd.RoundRect(p, c, h - new Vector2(10.5f, 10.5f), 5f), 0.8f, Gold.A(0.5f));
            Corners(img, 11f, 5f, GoldLight);
            img.Under(p => Sd.RoundRect(p - new Vector2(0f, 3f), c, h, 12f), 5f, C(0, 0, 0, 0.5f));
            return img;
        }

        private static Img Ribbon()
        {
            var img = new Img(26, 128);
            img.Paint(p =>
            {
                float notch = p.y < 14f ? Mathf.Abs(p.x - 13f) * 1.1f - (14f - p.y) : -1f;
                if (p.x < 2f || p.x > 24f || notch < 0f && p.y < 14f) return Color.clear;
                float shade = 0.85f + 0.15f * Mathf.Sin(p.x / 26f * Mathf.PI);
                return C((int)(150 * shade), (int)(36 * shade), (int)(52 * shade));
            });
            return img;
        }

        private static Img Seal()
        {
            var img = new Img(96, 96);
            Vector2 c = img.Center;
            Func<Vector2, float> blob = p =>
            {
                Vector2 d = p - c;
                float a = Mathf.Atan2(d.y, d.x);
                float wobble = Mathf.Sin(a * 7f) * 1.8f + Mathf.Sin(a * 11f + 1.3f) * 1.2f + Mathf.Sin(a * 3f + 0.4f) * 1.5f;
                return d.magnitude - (40f + wobble);
            };
            img.Under(p => blob(p - new Vector2(1.5f, -2.5f)), 3f, C(0, 0, 0, 0.45f));
            img.FillGradient(blob, C(176, 48, 74), C(108, 22, 48));
            img.Stroke(p => Sd.Circle(p, c, 29f), 3f, C(90, 16, 38, 0.8f));
            img.Stroke(p => Sd.Circle(p, c + new Vector2(-0.8f, 0.8f), 29f), 1.2f, C(230, 120, 140, 0.45f));
            img.Fill(p => Sd.Star4(p, c, 18f, 4.5f), C(96, 16, 40));
            img.Fill(p => Sd.Star4(p, c + new Vector2(-0.6f, 0.8f), 17f, 4f), C(214, 98, 118, 0.85f));
            img.Fill(p => Sd.Circle(p, c + new Vector2(-16f, 18f), 6f) , C(255, 220, 230, 0.18f));
            return img;
        }

        private static Img ButtonFrame()
        {
            var img = new Img(64, 48);
            Vector2 c = img.Center; Vector2 h = new Vector2(30f, 22f);
            img.Under(p => Sd.RoundRect(p - new Vector2(0f, -2f), c, h, 10f), 2.5f, C(0, 0, 0, 0.4f));
            img.FillGradient(p => Sd.RoundRect(p, c, h, 10f), C(104, 64, 140), C(52, 28, 76));
            img.Stroke(p => Sd.RoundRect(p, c, h, 10f), 2f, Gold);
            img.Stroke(p => Sd.RoundRect(p, c, h - new Vector2(4f, 4f), 7f), 0.8f, GoldLight.A(0.4f));
            return img;
        }

        private static Img Tab()
        {
            var img = new Img(40, 48);
            Vector2 c = img.Center; Vector2 h = new Vector2(18f, 22f);
            img.FillGradient(p => Sd.RoundRect(p, c, h, 9f), C(92, 56, 126, 0.95f), C(44, 24, 64, 0.95f));
            img.Stroke(p => Sd.RoundRect(p, c, h, 9f), 1.8f, Gold);
            return img;
        }

        /// <summary>Rombos dorados en las cuatro esquinas (la filigrana del marco).</summary>
        private static void Corners(Img img, float inset, float size, Color color)
        {
            float w = img.W, h = img.H;
            Vector2[] at = { new Vector2(inset, inset), new Vector2(w - inset, inset), new Vector2(inset, h - inset), new Vector2(w - inset, h - inset) };
            foreach (Vector2 p0 in at)
            {
                img.Fill(p => Sd.Diamond(p, p0, size), color);
                img.Stroke(p => Sd.Diamond(p, p0, size), 0.7f, GoldDark);
            }
        }

        // ---------------------------------------------------------------- guardar e importar

        private static void Save(string name, Img img, int border)
        {
            string path = $"{Folder}/{name}.png";
            var tex = new Texture2D(img.W, img.H, TextureFormat.RGBA32, false);
            tex.SetPixels(img.Px);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spriteBorder = new Vector4(border, border, border, border);
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.sRGBTexture = true;
            importer.SaveAndReimport();
        }

        private static Color C(int r, int g, int b, float a = 1f) => new Color(r / 255f, g / 255f, b / 255f, a);

        // ---------------------------------------------------------------- lienzo y distancias

        private sealed class Img
        {
            public readonly int W, H;
            public readonly Color[] Px;
            public Vector2 Center => new Vector2(W / 2f, H / 2f);

            public Img(int w, int h) { W = w; H = h; Px = new Color[w * h]; }

            /// <summary>Pinta con una funcion por pixel (centro del pixel, Y hacia arriba). Over = compone encima.</summary>
            public void Paint(Func<Vector2, Color> f, bool over = true)
            {
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                    {
                        Color src = f(new Vector2(x + 0.5f, y + 0.5f));
                        int i = y * W + x;
                        Px[i] = over ? Over(src, Px[i]) : src;
                    }
            }

            public void Fill(Func<Vector2, float> sdf, Color color) => Paint(p => color.A(color.a * Cov(sdf(p))));

            public void Stroke(Func<Vector2, float> sdf, float width, Color color) =>
                Paint(p => color.A(color.a * Cov(Mathf.Abs(sdf(p)) - width * 0.5f)));

            /// <summary>Degradado vertical (arriba -> abajo) dentro de la forma.</summary>
            public void FillGradient(Func<Vector2, float> sdf, Color top, Color bottom) =>
                Paint(p => { Color c = Color.Lerp(bottom, top, p.y / H); return c.A(c.a * Cov(sdf(p))); });

            /// <summary>Papel: dos tonos mezclados con ruido suave y grano fino.</summary>
            public void FillParchment(Func<Vector2, float> sdf, Color light, Color dark, float grain) =>
                Paint(p =>
                {
                    float n = Noise.Fbm(p * 0.09f);
                    Color c = Color.Lerp(light, dark, Mathf.Clamp01(n * 1.2f - 0.1f));
                    float g = (Noise.Hash(p.x, p.y) - 0.5f) * grain;
                    c = new Color(c.r + g, c.g + g, c.b + g * 0.8f, c.a);
                    return c.A(c.a * Cov(sdf(p)));
                });

            /// <summary>Sombra difusa por debajo de lo ya pintado.</summary>
            public void Under(Func<Vector2, float> sdf, float blur, Color color)
            {
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                    {
                        float d = sdf(new Vector2(x + 0.5f, y + 0.5f));
                        float a = Mathf.Clamp01(1f - Mathf.Max(0f, d) / blur) * color.a;
                        int i = y * W + x;
                        Px[i] = Over(Px[i], color.A(a));
                    }
            }

            private static float Cov(float d) => Mathf.Clamp01(0.5f - d);

            private static Color Over(Color src, Color dst)
            {
                float a = src.a + dst.a * (1f - src.a);
                if (a <= 1e-5f) return new Color(0f, 0f, 0f, 0f);
                Color c = (src * src.a + dst * dst.a * (1f - src.a)) / a;
                c.a = a;
                return c;
            }
        }

        private static Color A(this Color c, float a) { c.a = a; return c; }

        private static class Sd
        {
            public static float RoundRect(Vector2 p, Vector2 c, Vector2 half, float r)
            {
                Vector2 q = new Vector2(Mathf.Abs(p.x - c.x), Mathf.Abs(p.y - c.y)) - half + new Vector2(r, r);
                return Vector2.Max(q, Vector2.zero).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - r;
            }

            public static float Circle(Vector2 p, Vector2 c, float r) => (p - c).magnitude - r;

            public static float Diamond(Vector2 p, Vector2 c, float r) => (Mathf.Abs(p.x - c.x) + Mathf.Abs(p.y - c.y) - r) * 0.7071f;

            public static float Segment(Vector2 p, Vector2 a, Vector2 b)
            {
                Vector2 pa = p - a, ba = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(pa, ba) / ba.sqrMagnitude);
                return (pa - ba * t).magnitude;
            }

            /// <summary>Estrella de 4 puntas: union de dos rombos alargados.</summary>
            public static float Star4(Vector2 p, Vector2 c, float r, float w)
            {
                Vector2 d = new Vector2(Mathf.Abs(p.x - c.x), Mathf.Abs(p.y - c.y));
                float a = d.x / w + d.y / r - 1f;
                float b = d.x / r + d.y / w - 1f;
                return Mathf.Min(a, b) * w * 0.9f;
            }

            /// <summary>Gota: circulo abajo y punta arriba a altura total h.</summary>
            public static float Drop(Vector2 p, Vector2 c, float r, float h)
            {
                Vector2 d = p - c;
                float circle = d.magnitude - r;
                if (d.y <= 0f) return circle;
                float t = d.y / (h - r);
                if (t >= 1f) return Mathf.Min(circle, (d - new Vector2(0f, h - r)).magnitude);
                // Por encima del centro el ancho se afina hasta la punta (aproximacion de distancia, alcanza para el AA).
                float width = r * Mathf.Pow(1f - t, 1.3f);
                return Mathf.Min(circle, (Mathf.Abs(d.x) - width) * 0.85f);
            }
        }

        private static class Noise
        {
            public static float Hash(float x, float y)
            {
                float h = Mathf.Sin(x * 127.1f + y * 311.7f) * 43758.5453f;
                return h - Mathf.Floor(h);
            }

            private static float Value(Vector2 p)
            {
                float ix = Mathf.Floor(p.x), iy = Mathf.Floor(p.y);
                float fx = p.x - ix, fy = p.y - iy;
                fx = fx * fx * (3f - 2f * fx); fy = fy * fy * (3f - 2f * fy);
                float a = Hash(ix, iy), b = Hash(ix + 1f, iy), c = Hash(ix, iy + 1f), d = Hash(ix + 1f, iy + 1f);
                return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
            }

            public static float Fbm(Vector2 p) => Value(p) * 0.55f + Value(p * 2.1f) * 0.3f + Value(p * 4.3f) * 0.15f;
        }
    }
}
