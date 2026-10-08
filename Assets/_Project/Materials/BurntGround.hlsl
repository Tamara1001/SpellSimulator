// Suelo quemado del incendio de pasto (GrassFire). Lo incluye StylizedLit: el terreno y el pasto (mismo material) se
// pintan de ceniza donde el mapa global _GrassBurnTex lo indica, asi la quemadura sigue exacto el relieve low-poly,
// recibe luz y sombras de URP y las matas quemadas quedan carbonizadas. Sin geometria extra ni z-fighting.
// _GrassBurnTex (RGBAHalf, 1 texel por celda de 2 m, filtrado bilineal): r = quemado (0..1), g = calor de brasas
// (0..1), b = altura del suelo en la celda. _GrassBurnParams: xy = origen XZ de la grilla, z = 1 / lado (m).
// El borde es organico: el UV se deforma con fbm de la posicion mundial y el umbral tambien, asi celdas vecinas se
// funden sin costuras ni cuadricula. Centro negro-ceniza con motas grises, banda marron-rojiza tostada y brasas que
// laten mientras hay calor. Solo existe con la keyword global _GRASS_BURN (GrassFire la enciende si hay algo quemado).
#ifndef SPELLSIM_BURNT_GROUND_INCLUDED
#define SPELLSIM_BURNT_GROUND_INCLUDED

TEXTURE2D(_GrassBurnTex);
SAMPLER(sampler_GrassBurnTex);
float4 _GrassBurnParams;

float BurntHash(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

float BurntNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = BurntHash(i);
    float b = BurntHash(i + float2(1, 0));
    float c = BurntHash(i + float2(0, 1));
    float d = BurntHash(i + float2(1, 1));
    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

/// Oscurece albedo con la quemadura y devuelve la emision de las brasas (sumar despues de la luz).
half3 ApplyBurntGround(float3 positionWS, inout half3 albedo)
{
    float2 xz = positionWS.xz;
    float2 guv = (xz - _GrassBurnParams.xy) * _GrassBurnParams.z;
    if (any(guv < 0.0) || any(guv > 1.0)) return 0;

    // fbm de 3 octavas (0..1) para el contorno y una cuarta fina para las motas.
    float n = BurntNoise(xz * 0.35) * 0.55 + BurntNoise(xz * 0.9 + 17.3) * 0.3 + BurntNoise(xz * 2.3 - 5.1) * 0.15;
    float2 warp = float2(n - 0.5, BurntNoise(xz * 0.5 + 41.7) - 0.5) * (2.4 * _GrassBurnParams.z);
    half4 burn = SAMPLE_TEXTURE2D(_GrassBurnTex, sampler_GrassBurnTex, guv + warp);
    if (burn.r + burn.g < 0.01) return 0;

    // Solo cerca del suelo: pasto y base de troncos si, muebles altos no.
    float h = positionWS.y - burn.b;
    float near = 1.0 - smoothstep(0.3, 1.0, h);
    float v = burn.r + (n - 0.5) * 0.55;
    float scorch = smoothstep(0.42, 0.6, v) * near;
    float toast = smoothstep(0.24, 0.44, v) * near;

    // Motas de ceniza gris: chicas y ralas (escala ~8 cm) sobre un negro que varia apenas con el fbm grande.
    float fine = BurntNoise(xz * 12.0);
    float motes = smoothstep(0.84, 0.93, fine);
    motes *= saturate(1.5 - length(fwidth(xz)) * 12.0); // de lejos se apagan (sin centelleo)
    half3 ash = lerp(half3(0.016, 0.014, 0.013), half3(0.05, 0.045, 0.042), n);
    ash = lerp(ash, half3(0.2, 0.19, 0.18), motes * 0.45);
    albedo = lerp(albedo, half3(0.22, 0.1, 0.05), toast * 0.8);
    albedo = lerp(albedo, ash, scorch);

    // Brasas: grietas finas que laten mientras queda calor.
    // Ruido "ridged" (1 - |2n - 1|): lineas finas en vez de manchas; el parpadeo lento las enciende por tramos.
    float ridge = 1.0 - abs(BurntNoise(xz * 2.6 + 9.0) * 2.0 - 1.0);
    float flicker = BurntNoise(xz * 1.3 + _Time.y * 0.7);
    float cracks = smoothstep(0.955, 0.985, ridge) * smoothstep(0.5, 0.8, flicker);
    float ember = burn.g * burn.g * cracks * scorch;
    return half3(1.0, 0.3, 0.05) * (ember * 0.9);
}

#endif
