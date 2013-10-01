Shader "Chickenlord/Monster/Opaque Lite" {
Properties {
	_Color ("Main Color", Color) = (1,1,1,1)
	_SpecColor ("Specular Color", Color) = (0.5,0.5,0.5,1)
	_Shininess ("Shininess", Range(0.01,1)) = 0.078125
	_Specularity("Spec Intensity",Range(0,1)) = 0.7
	_ReflectColor ("Reflection Color", Color) = (1,1,1,0.5)
	_Fresnel("Reflection Fresnel Exponent",Range(0,6)) = 3
	_IFresnel("Reflection View Exponent",Range(0,6)) = 2
	_Glossfac("Reflection Softness",Range(0,1)) = 0
	_RefFresMul("Reflection Fresnel intensity",Range(0,2)) = 0.2
	_RefMul("Reflection View Intensity",Range(0,2)) = 0.66
	_MainTex ("Base (RGB) Gloss (A)", 2D) = "white" {}
	_Cube ("Reflection Cubemap", Cube) = "" { TexGen CubeReflect }
	_BumpMap ("Normalmap", 2D) = "bump" {}
}

SubShader {
	Tags { "RenderType"="Opaque" }
	LOD 400
CGPROGRAM
#pragma surface surf Monster fullforwardshadows addshadow
#pragma target 3.0
#pragma debug
#define PI 3.14159265359

sampler2D _MainTex;
sampler2D _BumpMap;
samplerCUBE _Cube;
fixed _Specularity;

fixed4 _Color;
fixed4 _ReflectColor;
half _Shininess;
half _Fresnel;
half _IFresnel;
half _RefFresMul;
half _RefMul;

struct Input {
	float2 uv_MainTex;
	float2 uv_BumpMap;
	float3 worldRefl;
	//float4 screenPos;
	float3 viewDir;
	INTERNAL_DATA
};

inline fixed4 LightingMonster (SurfaceOutput s, fixed3 lightDir, half3 viewDir, fixed atten)
{
	half3 h = normalize (lightDir + viewDir);
	
	fixed diff = (dot (s.Normal, lightDir));
	
	diff = saturate(diff);
	
	half nh = saturate(dot (s.Normal, h));
	half econ = (2+s.Specular*128)/(2*PI);
	half spec = pow (nh, s.Specular*128.0) * s.Gloss*econ;
	
	fixed4 c;
	c.rgb = _LightColor0.rgb * (s.Albedo * diff +  _SpecColor.rgb * spec) * (atten * 2);
	c.a = s.Alpha + _LightColor0.a * _SpecColor.a * spec * atten;
	return c;
}

inline half4 LightingMonster_DirLightmap (SurfaceOutput s, fixed4 color, fixed4 scale, half3 viewDir, bool surfFuncWritesNormal, out half3 specColor)
{
	UNITY_DIRBASIS
	half3 scalePerBasisVector;
	
	half3 lm = DirLightmapDiffuse (unity_DirBasis, color, scale, s.Normal, surfFuncWritesNormal, scalePerBasisVector);
	
	half3 lightDir = normalize (scalePerBasisVector.x * unity_DirBasis[0] + scalePerBasisVector.y * unity_DirBasis[1] + scalePerBasisVector.z * unity_DirBasis[2]);
	half3 h = normalize (lightDir + viewDir);
	
	half nh = saturate(dot (s.Normal, h));
	half econ = (2+s.Specular*128)/(2*PI);
	half spec = pow (nh, s.Specular*128.0) * econ;
	
	// specColor used outside in the forward path, compiled out in prepass
	specColor = lm * _SpecColor.rgb * s.Gloss * spec;
	
	// spec from the alpha component is used to calculate specular
	// in the Lighting*_Prepass function, it's not used in forward
	
	return half4(lm, spec);
}

inline fixed4 LightingMonster_PrePass (SurfaceOutput s, half4 light)
{
	fixed spec = light.a * s.Gloss;
	half econ = (2+s.Specular*128)/(2*PI);
	
	fixed4 c;
	c.rgb = (s.Albedo * light.rgb + light.rgb * _SpecColor.rgb * spec);
	c.a = s.Alpha + spec * _SpecColor.a*econ;
	return c;
}

void surf (Input IN, inout SurfaceOutput o) 
{
	fixed4 tex = tex2D(_MainTex, IN.uv_MainTex);
	fixed4 c = tex * _Color;
	o.Albedo = c.rgb;
	
	o.Gloss = _Specularity*tex.a;
	o.Specular = _Shininess;
	o.Normal = UnpackNormal(tex2D(_BumpMap, IN.uv_BumpMap));
	
	float3 worldRefl = WorldReflectionVector (IN, o.Normal);
	fixed4 reflcol = texCUBE(_Cube, ((worldRefl)));
	//fixed4 reflcol = tex2D (_ReflectionTex, IN.screenPos.xy / IN.screenPos.w);
	reflcol.a =Luminance(reflcol);
	
	half vdn = saturate(dot(normalize(IN.viewDir),o.Normal));
	half refV = min(2,pow(vdn,_IFresnel)*_RefMul+pow((1 - vdn),_Fresnel)*_RefFresMul);
	
	o.Emission = reflcol.rgb * _ReflectColor.rgb * half3(refV,refV,refV);
	
	o.Emission *= tex.a;
	
	fixed refLum = Luminance(_ReflectColor.rgb)*reflcol.a*(refV);
	
	half added = _RefFresMul+_RefMul;
	o.Albedo *= max(0,1-refLum);
	o.Gloss *= max(0,1-refLum);
	
	o.Alpha = c.a;
}
ENDCG
}

FallBack "Reflective/Bumped Diffuse"
}