using UnityEngine;

namespace DjVisuals
{
    /// <summary>Material factory for the URP pipeline. All HDR colors (components above 1) feed bloom.</summary>
    public static class Mats
    {
        static Shader Get(string name)
        {
            var s = Shader.Find(name);
            if (s == null) Debug.LogError("[DjVisuals] Shader not found: " + name);
            return s;
        }

        public static Material Lit(Color c, float smooth = 0.5f, float metal = 0f)
        {
            var m = new Material(Get("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", metal);
            return m;
        }

        /// <summary>Flat HDR color, for suns (bloom does the glow).</summary>
        public static Material Unlit(Color hdr)
        {
            var m = new Material(Get("Universal Render Pipeline/Unlit"));
            m.SetColor("_BaseColor", hdr);
            return m;
        }

        public static void Emission(Material m, Color hdr)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", hdr);
        }

        public static Material Additive(Color hdr, float soft = 1.5f)
        {
            var m = new Material(Get("DjVisuals/Additive"));
            m.SetColor("_Color", hdr);
            m.SetFloat("_Soft", soft);
            return m;
        }

        public static Material Trail(Color hdr, float soft = 1.2f)
        {
            var m = new Material(Get("DjVisuals/AdditiveTrail"));
            m.SetColor("_Color", hdr);
            m.SetFloat("_Soft", soft);
            return m;
        }

        public static Material Planet(Color a, Color b, float bands, float bandAmt, float noise, Color atmo, float atmoInt, float seed)
        {
            var m = new Material(Get("DjVisuals/Planet"));
            m.SetColor("_ColA", a);
            m.SetColor("_ColB", b);
            m.SetFloat("_Bands", bands);
            m.SetFloat("_BandAmt", bandAmt);
            m.SetFloat("_Noise", noise);
            m.SetColor("_Atmo", atmo);
            m.SetFloat("_AtmoInt", atmoInt);
            m.SetFloat("_Seed", seed);
            return m;
        }

        public static Material Ring(Color c)
        {
            var m = new Material(Get("DjVisuals/Ring"));
            m.SetColor("_Color", c);
            return m;
        }

        public static Material Accretion()
        {
            return new Material(Get("DjVisuals/Accretion"));
        }

        public static Material PhotonRing(Color hdr)
        {
            var m = new Material(Get("DjVisuals/PhotonRing"));
            m.SetColor("_Color", hdr);
            return m;
        }

        public static Material WaterSky() { return new Material(Get("DjVisuals/WaterSky")); }

        public static Material Seabed(Color sand, Color tint, Color glow, float coralMode)
        {
            var m = new Material(Get("DjVisuals/Seabed"));
            m.SetColor("_Sand", sand);
            m.SetColor("_Tint", tint);
            m.SetColor("_Glow", glow);
            m.SetFloat("_CoralMode", coralMode);
            return m;
        }

        public static Material Jelly(Color hdr)
        {
            var m = new Material(Get("DjVisuals/Jelly"));
            m.SetColor("_Color", hdr);
            return m;
        }

        public static Material Fish(Color back, Color belly, Color fin)
        {
            var m = new Material(Get("DjVisuals/Fish"));
            m.SetColor("_Color", back);
            m.SetColor("_Belly", belly);
            m.SetColor("_Fin", fin);
            return m;
        }

        /// <summary>Flat-shaded faceted material (creatures and reef pieces). Albedo = vertex color x tint.</summary>
        public static Material Facet(Color tint, Color glow)
        {
            var m = new Material(Get("DjVisuals/Facet"));
            m.SetColor("_Tint", tint);
            m.SetColor("_Glow", glow);
            return m;
        }

        /// <summary>Faceted bubble: clear with a bright rim. fill = how solid the middle is.</summary>
        public static Material Bubble(Color hdr, float fill, bool wall)
        {
            var m = new Material(Get("DjVisuals/Bubble"));
            m.SetColor("_Color", hdr);
            m.SetFloat("_Fill", fill);
            m.SetFloat("_Wall", wall ? 1f : 0f);
            return m;
        }

        public static Material NebulaSky()
        {
            return new Material(Get("DjVisuals/NebulaSky"));
        }

        /// <summary>Gives every renderer under the object one lit material using a palette texture.</summary>
        public static void ApplyTexture(GameObject go, Texture2D tex, float smooth = 0.4f, float metal = 0.25f)
        {
            var mat = Lit(Color.white, smooth, metal);
            mat.SetTexture("_BaseMap", tex);
            foreach (var r in go.GetComponentsInChildren<Renderer>()) r.sharedMaterial = mat;
        }

        public static GameObject Prim(PrimitiveType type, Transform parent, string name, Vector3 localPos, Vector3 localScale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            Object.Destroy(go.GetComponent<Collider>());
            return go;
        }
    }
}
