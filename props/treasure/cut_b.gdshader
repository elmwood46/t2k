shader_type spatial;
render_mode cull_back, unshaded;

#include "res://props/treasure/coin/cut_distance.gdshaderinc"

uniform sampler2D _noise;
uniform sampler2D _spot_noise;
uniform vec3 burned_colour : source_color;
uniform mat4 cutplane;
varying vec3 wvtx;

void vertex() {
	wvtx = (MODEL_MATRIX * vec4(VERTEX,1.0)).xyz;
}

void fragment() {
	float dist = cutDistance(-cutplane[1].xyz, cutplane[3].xyz, wvtx, _noise);

	if (dist < 0.0) discard;

	float noiseval = texture(_spot_noise,UV).r;
	noiseval += 0.05*sin(TIME);
	noiseval = pow(noiseval,2);

	ALBEDO = burned_colour*noiseval;
	//EMISSION = (mix(vec4(color,1), vec4(0.0), dist)).rgb;
}

//void light() {
	// Called for every pixel for every light affecting the material.
	// Uncomment to replace the default light processing function with this one.
//}
