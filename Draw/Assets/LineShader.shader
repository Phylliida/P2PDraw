
Shader "Unlit/LineShader"
{
	Properties
	{
	}
	SubShader
	{
		Tags { "RenderType"="Opaque" }
		Lighting Off
		LOD 100

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 5.0
			#include "UnityCG.cginc"


			struct v2f
			{
				float4 vertex : SV_POSITION;
			};
			
			uniform StructuredBuffer<float3> segmentDatas;
			uniform StructuredBuffer<int> notConnectedToPrev;

			v2f vert(uint vid : SV_VertexID)
			{
				uint ptId = vid / 2;
				uint isNext = vid % 2;
				float3 pt = segmentDatas[ptId];
				int isConnectedToPrev = 1 - notConnectedToPrev[ptId+1];
				isNext = isConnectedToPrev*isNext;
				pt = segmentDatas[ptId] * (1 - isNext) + segmentDatas[ptId+1] * isNext;
				//pt = segmefnftDatas[ptId + 1];


				v2f o;
				o.vertex = UnityObjectToClipPos(float4(pt, 1));
				return o;
			}
			
			fixed4 frag (v2f i) : SV_Target
			{
				return fixed4(1,1,1,1);
			}
			ENDCG
		}
	}
}
