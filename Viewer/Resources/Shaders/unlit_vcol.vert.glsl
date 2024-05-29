#version 330

uniform mat4 model;
uniform mat4 view;
uniform mat4 projection;

layout (location = 0) in vec3 position;
layout (location = 3) in vec4 color;

out vec4 vColor;

void main()
{
	gl_Position = projection * view * model * vec4(position, 1.0);
	vColor = color;
}