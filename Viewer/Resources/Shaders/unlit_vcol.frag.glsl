#version 330

in vec4 vColor;

out vec4 FragColor;

void main()
{
	vec4 finalColor = vColor;
	// NOTE: This float value is exact
	finalColor.a = finalColor.a * 1.9921875f;

	if (finalColor.a < 0.01f)
		discard;

	float trueAlpha = finalColor.a;
	finalColor.a = min(finalColor.a, 1.0);

	FragColor = finalColor;
}