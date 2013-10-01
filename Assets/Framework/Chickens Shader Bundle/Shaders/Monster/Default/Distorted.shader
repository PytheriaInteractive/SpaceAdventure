// Upgrade NOTE: commented out 'float4 unity_ShadowFadeCenterAndType', a built-in variable

Shader "Chickenlord/Monster/Refractive" {
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
	_Index ("Per Vertex Refraction",Range(0,1)) = 0
	_BumpAmt  ("NormalMap Distortion", Range (0,128)) = 10
}

SubShader {
	Tags {"Queue"="Transparent+1" "IgnoreProjector"="True" "RenderType"="Opaque"}
	LOD 400
	
	GrabPass 
	{
		"_MGrabTex"
 	}
	
	Pass {
		Name "FORWARD"
		Tags { "LightMode" = "ForwardBase" }
		
		Blend off
		ZWrite On

CGPROGRAM
#pragma vertex vert_surf
#pragma fragment frag_surf
#pragma fragmentoption ARB_precision_hint_fastest
#pragma multi_compile_fwdbase
#include "HLSLSupport.cginc"
#define UNITY_PASS_FORWARDBASE
#include "UnityCG.cginc"
#include "Lighting.cginc"
#include "AutoLight.cginc"

#define INTERNAL_DATA half3 TtoW0; half3 TtoW1; half3 TtoW2;
#define WorldReflectionVector(data,normal) reflect (data.worldRefl, half3(dot(data.TtoW0,normal), dot(data.TtoW1,normal), dot(data.TtoW2,normal)))
#define WorldNormalVector(data,normal) fixed3(dot(data.TtoW0,normal), dot(data.TtoW1,normal), dot(data.TtoW2,normal))
#line 1
#line 28

//#pragma surface surf Monster fullforwardshadows addshadow approxview
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

sampler2D _MGrabTex;
half _Index;
half _BumpAmt;
float4 _MGrabTex_TexelSize;


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
	half4 uvgrab;
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
	//o.Gloss *= max(0,1 - elum*refV);
	o.Albedo *= defac*min(1,added)+max(0,1-added);
	//o.Gloss *= max(0,1-refLum);
	o.Gloss *= defac*min(1,added)+max(0,1-added);
	//o.Gloss *= max(0,1 - elum*refV);
	
	o.MCD = (_Metallic*mcd)*2.44+(1-_Metallic);
	
	o.Alpha = max(c.a,tex.a*refV*_ReflectColor.a);
	
	
	half2 bump = o.Normal.rg; // we could optimize this by just reading the x & y without reconstructing the Z
	float2 offset = bump * _BumpAmt * _MGrabTex_TexelSize.xy;
	IN.uvgrab.xy = offset * IN.uvgrab.z + IN.uvgrab.xy;
	IN.uvgrab.xy /= IN.uvgrab.w;
	IN.uvgrab.xy = saturate(IN.uvgrab.xy);
	
	half4 grabcol = tex2D( _MGrabTex, IN.uvgrab.xy);
	o.Albedo *= o.Alpha;
	o.Emission *= o.Alpha;
	o.Gloss *= o.Alpha;
	o.Emission += (1-o.Alpha)*grabcol;
	o.Alpha = 1;
}
#ifdef LIGHTMAP_OFF
struct v2f_surf {
  float4 pos : SV_POSITION;
  float4 pack0 : TEXCOORD0;
  fixed3 viewDir : TEXCOORD1;
  fixed4 TtoW0 : TEXCOORD2;
  fixed4 TtoW1 : TEXCOORD3;
  fixed4 TtoW2 : TEXCOORD4;
  half4 lightDir : TEXCOORD5;
  half4 vlight : TEXCOORD6;
  LIGHTING_COORDS(7,8)
};
#endif
#ifndef LIGHTMAP_OFF
struct v2f_surf {
  float4 pos : SV_POSITION;
  float4 pack0 : TEXCOORD0;
  fixed3 viewDir : TEXCOORD1;
  fixed4 TtoW0 : TEXCOORD2;
  fixed4 TtoW1 : TEXCOORD3;
  fixed4 TtoW2 : TEXCOORD4;
  float4 lmap : TEXCOORD5;
  LIGHTING_COORDS(6,7)
};
#endif
#ifndef LIGHTMAP_OFF
float4 unity_LightmapST;
// float4 unity_ShadowFadeCenterAndType;
#endif
float4 _MainTex_ST;
float4 _BumpMap_ST;
v2f_surf vert_surf (appdata_full v) {
  v2f_surf o;
  o.pos = mul (UNITY_MATRIX_MVP, v.vertex);
  o.pack0.xy = TRANSFORM_TEX(v.texcoord, _MainTex);
  float3 viewDir = -ObjSpaceViewDir(v.vertex);
  float3 worldRefl = mul ((float3x3)_Object2World, viewDir);
  
   float3 objSpaceCameraPos = mul(_World2Object, float4(_WorldSpaceCameraPos.xyz, 1)).xyz * unity_Scale.w;
	viewDir = objSpaceCameraPos - v.vertex.xyz;
	float length  = distance(objSpaceCameraPos,v.vertex.xyz);
	float3 viewNorm = (viewDir);
	float3 refracted = refract(-normalize(viewDir.xyz),normalize(SCALED_NORMAL),1-_Index);
	refracted = (refracted)+objSpaceCameraPos-viewNorm;
	float4 uvgrab = ComputeGrabScreenPos(mul(UNITY_MATRIX_MVP, float4(refracted,v.vertex.w)));
	o.pack0.zw = uvgrab.xy;
	#ifndef LIGHTMAP_OFF
	o.lmap.zw = uvgrab.zw;
	#else
	o.lightDir.w = uvgrab.z;
	o.vlight.w = uvgrab.w;
	#endif
  
  TANGENT_SPACE_ROTATION;
  o.TtoW0 = float4(mul(rotation, _Object2World[0].xyz), worldRefl.x)*unity_Scale.w;
  o.TtoW1 = float4(mul(rotation, _Object2World[1].xyz), worldRefl.y)*unity_Scale.w;
  o.TtoW2 = float4(mul(rotation, _Object2World[2].xyz), worldRefl.z)*unity_Scale.w;
  #ifndef LIGHTMAP_OFF
  o.lmap.xy = v.texcoord1.xy * unity_LightmapST.xy + unity_LightmapST.zw;
  #endif
  float3 worldN = mul((float3x3)_Object2World, SCALED_NORMAL);
  float3 lightDir = mul (rotation, ObjSpaceLightDir(v.vertex));
  #ifdef LIGHTMAP_OFF
  o.lightDir.xyz = lightDir;
  #endif
  float3 viewDirForLight = mul (rotation, ObjSpaceViewDir(v.vertex));
  o.viewDir = normalize(viewDirForLight);
  #ifdef LIGHTMAP_OFF
  float3 shlight = ShadeSH9 (float4(worldN,1.0));
  o.vlight.xyz = shlight;
  #ifdef VERTEXLIGHT_ON
  float3 worldPos = mul(_Object2World, v.vertex).xyz;
  o.vlight.xyz += Shade4PointLights (
    unity_4LightPosX0, unity_4LightPosY0, unity_4LightPosZ0,
    unity_LightColor[0].rgb, unity_LightColor[1].rgb, unity_LightColor[2].rgb, unity_LightColor[3].rgb,
    unity_4LightAtten0, worldPos, worldN );
  #endif // VERTEXLIGHT_ON
  #endif // LIGHTMAP_OFF
  TRANSFER_VERTEX_TO_FRAGMENT(o);
  return o;
}
#ifndef LIGHTMAP_OFF
sampler2D unity_Lightmap;
#ifndef DIRLIGHTMAP_OFF
sampler2D unity_LightmapInd;
#endif
#endif
fixed4 frag_surf (v2f_surf IN) : COLOR {
  Input surfIN;
  surfIN.uv_MainTex = IN.pack0.xy;
  surfIN.uv_BumpMap = IN.pack0.xy;
  surfIN.worldRefl = float3(IN.TtoW0.w, IN.TtoW1.w, IN.TtoW2.w);
  surfIN.TtoW0 = IN.TtoW0.xyz;
  surfIN.TtoW1 = IN.TtoW1.xyz;
  surfIN.TtoW2 = IN.TtoW2.xyz;
  surfIN.viewDir = IN.viewDir;
  
  #ifdef LIGHTMAP_OFF
  surfIN.uvgrab = float4(IN.pack0.z,IN.pack0.w,IN.lightDir.w,IN.vlight.w);
  #else
  surfIN.uvgrab = float4(IN.pack0.z,IN.pack0.w,IN.lmap.z,IN.lmap.w);
  #endif
  
  SurfaceOutputPS o;
  o.Albedo = 0.0;
  o.Emission = 0.0;
  o.Specular = 0.0;
  o.Alpha = 0.0;
  surf (surfIN, o);
  fixed atten = LIGHT_ATTENUATION(IN);
  fixed4 c = 0;
  #ifdef LIGHTMAP_OFF
  c = LightingMonster (o, IN.lightDir.xyz, IN.viewDir, atten);
  #endif // LIGHTMAP_OFF
  #ifdef LIGHTMAP_OFF
  c.rgb += o.Albedo * IN.vlight.xyz;
  #endif // LIGHTMAP_OFF
  #ifndef LIGHTMAP_OFF
  #ifdef DIRLIGHTMAP_OFF
  fixed4 lmtex = tex2D(unity_Lightmap, IN.lmap.xy);
  fixed3 lm = DecodeLightmap (lmtex);
  #else
  half3 specColor;
  fixed4 lmtex = tex2D(unity_Lightmap, IN.lmap.xy);
  fixed4 lmIndTex = tex2D(unity_LightmapInd, IN.lmap.xy);
  half3 lm = LightingMonster_DirLightmap(o, lmtex, lmIndTex, IN.viewDir, 1, specColor).rgb;
  c.rgb += specColor;
  #endif
  #ifdef SHADOWS_SCREEN
  #if defined(SHADER_API_GLES) && defined(SHADER_API_MOBILE)
  c.rgb += o.Albedo * min(lm, atten*2);
  #else
  c.rgb += o.Albedo * max(min(lm,(atten*2)*lmtex.rgb), lm*atten);
  #endif
  #else // SHADOWS_SCREEN
  c.rgb += o.Albedo * lm;
  #endif // SHADOWS_SCREEN
  c.a = o.Alpha;
#endif // LIGHTMAP_OFF
  c.rgb += o.Emission;
  return c;
}

ENDCG
}
	Pass {
		Name "FORWARD"
		Tags { "LightMode" = "ForwardAdd" }
		ZWrite Off Blend SrcAlpha One Fog { Color (0,0,0,0) }
		Blend SrcAlpha One

CGPROGRAM
#pragma vertex vert_surf
#pragma fragment frag_surf
#pragma fragmentoption ARB_precision_hint_fastest
#pragma multi_compile_fwdadd_fullshadows
#include "HLSLSupport.cginc"
#define UNITY_PASS_FORWARDADD
#include "UnityCG.cginc"
#include "Lighting.cginc"
#include "AutoLight.cginc"

#define INTERNAL_DATA half3 TtoW0; half3 TtoW1; half3 TtoW2;
#define WorldReflectionVector(data,normal) reflect (data.worldRefl, half3(dot(data.TtoW0,normal), dot(data.TtoW1,normal), dot(data.TtoW2,normal)))
#define WorldNormalVector(data,normal) fixed3(dot(data.TtoW0,normal), dot(data.TtoW1,normal), dot(data.TtoW2,normal))
#line 1
#line 28

//#pragma surface surf Monster fullforwardshadows addshadow approxview
#pragma target 3.0
#pragma debug
#define PI 3.14159265359

sampler2D _MainTex;
sampler2D _BumpMap;
samplerCUBE _Cube;
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
	o.Albedo = c.rgb;
	
	o.Gloss = _Specularity*tex.a;
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
	
	o.Emission *= tex.a;
	o.Emission *= ((1-_Metallic)+_Metallic*mcd)*(1+_ChromeFactor*(1+_Metallic));

	half elum = Luminance(o.Emission);
	fixed cfac = (1-_ChromeFactor);
	fixed refLum = Luminance(_ReflectColor.rgb)*reflcol.a;
	
	half defac= max(cfac*0.5,0.5-refV*refLum*tex.a)*(cfac+1);
	
	half added = _RefFresMul+_RefMul;
	o.Albedo *= defac*min(1,added)+max(0,1-added);
	o.Gloss *= defac*min(1,added)+max(0,1-added);
	
	o.MCD = (_Metallic*mcd)*2.44+(1-_Metallic);
	o.Alpha = c.a;
}
struct v2f_surf {
  float4 pos : SV_POSITION;
  float4 pack0 : TEXCOORD0;
  fixed3 viewDir : TEXCOORD1;
  fixed4 TtoW0 : TEXCOORD2;
  fixed4 TtoW1 : TEXCOORD3;
  fixed4 TtoW2 : TEXCOORD4;
  half3 lightDir : TEXCOORD5;
  LIGHTING_COORDS(6,7)
};
float4 _MainTex_ST;
float4 _BumpMap_ST;
v2f_surf vert_surf (appdata_full v) {
  v2f_surf o;
  o.pos = mul (UNITY_MATRIX_MVP, v.vertex);
  o.pack0.xy = TRANSFORM_TEX(v.texcoord, _MainTex);
  o.pack0.zw = TRANSFORM_TEX(v.texcoord, _BumpMap);
  float3 viewDir = -ObjSpaceViewDir(v.vertex);
  float3 worldRefl = mul ((float3x3)_Object2World, viewDir);
  TANGENT_SPACE_ROTATION;
  o.TtoW0 = float4(mul(rotation, _Object2World[0].xyz), worldRefl.x)*unity_Scale.w;
  o.TtoW1 = float4(mul(rotation, _Object2World[1].xyz), worldRefl.y)*unity_Scale.w;
  o.TtoW2 = float4(mul(rotation, _Object2World[2].xyz), worldRefl.z)*unity_Scale.w;
  float3 lightDir = mul (rotation, ObjSpaceLightDir(v.vertex));
  o.lightDir = lightDir;
  float3 viewDirForLight = mul (rotation, ObjSpaceViewDir(v.vertex));
  o.viewDir = normalize (viewDirForLight);
  TRANSFER_VERTEX_TO_FRAGMENT(o);
  return o;
}
fixed4 frag_surf (v2f_surf IN) : COLOR {
  Input surfIN;
  surfIN.uv_MainTex = IN.pack0.xy;
  surfIN.uv_BumpMap = IN.pack0.zw;
  surfIN.worldRefl = float3(IN.TtoW0.w, IN.TtoW1.w, IN.TtoW2.w);
  surfIN.TtoW0 = IN.TtoW0.xyz;
  surfIN.TtoW1 = IN.TtoW1.xyz;
  surfIN.TtoW2 = IN.TtoW2.xyz;
  surfIN.viewDir = IN.viewDir;
  SurfaceOutputPS o;
  o.Albedo = 0.0;
  o.Emission = 0.0;
  o.Specular = 0.0;
  o.Alpha = 0.0;
  surf (surfIN, o);
  #ifndef USING_DIRECTIONAL_LIGHT
  fixed3 lightDir = normalize(IN.lightDir);
  #else
  fixed3 lightDir = IN.lightDir;
  #endif
  fixed4 c = LightingMonster (o, lightDir, IN.viewDir, LIGHT_ATTENUATION(IN));
  return c;
}

ENDCG
}
}
}