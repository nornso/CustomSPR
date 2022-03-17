#ifndef CUSTOM_UNLIT_COMMON_INCLUDED
#define CUSTOM_UNLIT_COMMON_INCLUDED

#include "UnityInput.hlsl"

float3 TransformObjectToWorld(float3 postitionOS){
	return mul(unity_ObjectToWorld,float4(postitionOS,1.0)).xyz;
}

float4 TransformWorldToHClip(float3 positionWS){
	return mul(unity_MatrixVP, float4(positionWS, 1.0));
}

#endif