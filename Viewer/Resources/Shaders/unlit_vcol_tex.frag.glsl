#version 330

uniform sampler2D tex_diffuse;
//uniform vec4 fog_color;
//uniform float fog_near;
//uniform float fog_far;
//uniform vec2 uv_offset;

in vec4 vColor;
in vec2 vTex;
in float vDepth;

out vec4 FragColor;

void main()
{

	vec4 texColor = texture2D(tex_diffuse, vTex);
	
	float texAlpha = texColor.a;
	texAlpha = texAlpha * 1.9921875f;

	float vertAlpha = vColor.a;
	vertAlpha = vertAlpha * 1.9921875f;

	float trueAlpha = vertAlpha * texAlpha;
	float effectiveAlpha = min(trueAlpha, 1.0);

	vec4 finalColor = texColor * vColor;
	finalColor.a = effectiveAlpha;
	
	if (trueAlpha < 0.01f)
		discard;
	
	// cheap "bloom" emulation
	if (trueAlpha > 1.0f)
		finalColor.rgb = finalColor.rgb * trueAlpha;
	
	FragColor = finalColor;

}