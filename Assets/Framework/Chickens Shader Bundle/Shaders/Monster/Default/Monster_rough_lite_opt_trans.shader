Shader "Chickenlord/Monster/Transparent" {
Properties {
	_Color ("Main Color", Color) = (1,1,1,1)
	_SpecColor ("Specular Color", Color) = (0.5,0.5,0.5,1)
	_Shininess ("Shininess", Range(0.01,1)) = 0.078125
	_Specularity("Spec Intensity",Range(0,1)) = 0.7
	_ReflectColor ("Reflection Color", Color) = (1,1,1,0.7)
	_Fresnel("Reflection Fresnel Exponent",Range(0,6)) = 3
	_IFresnel("Reflection View Exponent",Range(0,6)) = 2
	_Glossfac("Reflection Softness",Range(0,1)) = 0
	_RefFresMul("Reflection Fresnel intensity",Range(0,2)) = 0.2
	_RefMul("Reflection View Intensity",Range(0,2)) = 0.66
	_MainTex ("Base (RGB) Transparency (A)", 2D) = "white" {}
	_GlossMap("Gloss/Reflection (A)",2D) = ""{}
	_Cube ("Reflection Cubemap", Cube) = "" { TexGen CubeReflect }
	_BumpMap ("Normalmap", 2D) = "bump" {}
	_Metallic("MainTex as Spec/Reflection Color",Range(0,1)) = 0
	_ChromeFactor("Chrome factor",Range(0,1)) = 0
	_DiffFalloff("Diff Falloff",Range(1,32)) = 1
	_ScatterColor("Scatter Color",Color) = (1,1,1,1)
	_ScatFac("Scatter Range",Range(0,1)) = 0
	_ScatMul("Scatter Intensity",Range(0,1)) = 0
}

SubShader {
	Tags {"Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent"}
	LOD 400
	
	Pass
	{
		ZWrite On
		Colormask 0
	}
	
CGPROGRAM
#pragma surface surf Monster fullforwardshadows addshadow alpha
#pragma target 3.0
#pragma debug
#define PI 3.14159265359

sampler2D _MainTex;
sampler2D _BumpMap;
samplerCUBE _Cube;
sampler2D _GlossMap;
fixed _ChromeFactor;
half _DiffFalloff;
fixed _Specularity;

fixed4 _Color;
fixed4 _ReflectColor;
half _Shininess;
half _Fresnel;
half _IFresnel;
half _RefFresMul;
half _RefMul;
half _Metallic;
half _Glossfac;

fixed4 _ScatterColor;
fixed _ScatFac;
fixed _ScatMul;


struct SurfaceOutputPS {
	fixed3 Albedo;
	fixed3 Normal;
	fixed3 Emission;
	half Specular;
	fixed Gloss;
	fixed Alpha;
	fixed3 MCD;
};

struct Input {
	float2 uv_MainTex;
	float2 uv_BumpMap;
	float3 worldRefl;
	float3 viewDir;
	INTERNAL_DATA
};

inline fixed4 LightingMonster (SurfaceOutputPS s, fixed3 lightDir, half3 viewDir, fixed atten)
{
	half3 h = normalize (lightDir + viewDir);
	
	fixed diff = (dot (s.Normal, lightDir));
	fixed shadowCheck =(diff*0.5+0.5);
	
	float wrap = saturate((max(0,dot(s.Normal,lightDir)+_ScatFac)/(1+_ScatFac)));
	
	shadowCheck*=shadowCheck*shadowCheck*shadowCheck;
	shadowCheck = 1-shadowCheck;
	shadowCheck*=shadowCheck*shadowCheck*shadowCheck;
	shadowCheck = 1-shadowCheck;
	
	diff = saturate(diff);
	diff = pow(1-pow(1-diff,_DiffFalloff),_DiffFalloff);
	diff = diff*lerp(1,0.69,(_DiffFalloff-1)/31);
	
	half nh = saturate(dot (s.Normal, h));
	half econ = (2+s.Specular*128)/(2*PI);
	half spec = pow (nh, s.Specular*128.0) * s.Gloss*econ*shadowCheck;
	
	fixed4 c;
	c.rgb = _LightColor0.rgb * (s.Albedo * (diff+max(0,wrap-diff)*_ScatMul*_ScatterColor.rgb) +  _SpecColor.rgb * spec*s.MCD) * (atten * 2);
	c.a = s.Alpha + _LightColor0.a * _SpecColor.a * spec * atten;
	return c;
}


inline half4 LightingMonster_DirLightmap (SurfaceOutputPS s, fixed4 color, fixed4 scale, half3 viewDir, bool surfFuncWritesNormal, out half3 specColor)
{
	UNITY_DIRBASIS
	half3 scalePerBasisVector;
	
	half3 lm = DirLightmapDiffuse (unity_DirBasis, color, scale, s.Normal, surfFuncWritesNormal, scalePerBasisVector);
	
	half3 lightDir = normalize (scalePerBasisVector.x * unity_DirBasis[0] + scalePerBasisVector.y * unity_DirBasis[1] + scalePerBasisVector.z * unity_DirBasis[2]);
	half3 h = normalize (lightDir + viewDir);
	
	half nh = saturate(dot (s.Normal, h));
	half econ = (2+s.Specular*128)/(2*PI);
	half spec = pow (nh, s.Specular*128.0) * s.Gloss*econ;
	
	// specColor used outside in the forward path, compiled out in prepass
	specColor = lm * _SpecColor.rgb * s.Gloss * spec * s.MCD;
	
	// spec from the alpha component is used to calculate specular
	// in the Lighting*_Prepass function, it's not used in forward
	return half4(lm, spec);
}

void surf (Input IN, inout SurfaceOutputPS o) 
{
	fixed4 tex = tex2D(_MainTex, IN.uv_MainTex);
	fixed4 c = tex * _Color;
	fixed gloss = tex2D(_GlossMap,IN.uv_MainTex).a;
	o.Albedo = c.rgb;
	
	o.Gloss = _Specularity*gloss;
	o.Specular = _Shininess;
	o.Normal = UnpackNormal(tex2D(_BumpMap, IN.uv_BumpMap));
	half3 SNormal = normalize(lerp(o.Normal,half3(0,0,1),_Glossfac));
	
	float3 worldRefl = WorldReflectionVector (IN, SNormal);
	fixed4 reflcol = texCUBE(_Cube, ((worldRefl)));
	
	half vdn = saturate(dot(normalize(IN.viewDir),o.Normal));
	half refV = min(2,pow(vdn,_IFresnel)*_RefMul+pow((1 - vdn),_Fresnel)*_RefFresMul);
	
	o.Emission = reflcol.rgb * _ReflectColor.rgb * half3(refV,refV,refV);
	half3 mcd = c.rgb+half3(0.00133,0.00068,0.00351);
	half3 metCol = c.rgb+half3(0.01,0.01,0.01);
	half mix = metCol.r+metCol.g+metCol.b;
	mcd /= mix;
	
	o.Emission *= gloss;
	o.Emission *= ((1-_Metallic)+_Metallic*mcd)*(1+_ChromeFactor*(1+_Metallic));

	half elum = Luminance(o.Emission);
	fixed cfac = (1-_ChromeFactor);
	fixed refLum = Luminance(_ReflectColor.rgb)*reflcol.a;
	
	half defac= max(cfac*0.5,0.5-refV*refLum*gloss)*(cfac+1);
	
	half added = _RefFresMul+_RefMul;
	o.Albedo *= defac*min(1,added)+max(0,1-added);
	o.Gloss *= defac*min(1,added)+max(0,1-added);
	
	o.MCD = (_Metallic*mcd)*2.44+(1-_Metallic);
	o.Alpha = max(c.a,tex.a*refV*_ReflectColor.a);
}
ENDCG
}

FallBack "Reflective/Bumped Diffuse"
}