shader_type spatial;
render_mode cull_disabled;

uniform sampler2D _albedo : source_color;
uniform sampler2D _noise;
global uniform vec4 border_colour;
uniform mat4 cutplane;
varying vec3 wvtx;

void vertex() {
	wvtx = (MODEL_MATRIX * vec4(VERTEX,1.0)).xyz;
}

void fragment() {
	// cutplane is a transform matrix
	// use column 2, the rotation, as the cutting plane's normal
	vec3 planenormal = normalize(-cutplane[1].xyz);

	// column 4, the position, is the plane's position
	// use the dot product to get positions projected along normal vector
	float planedistance = dot(planenormal, cutplane[3].xyz);
	float vertexdistance = dot(planenormal, wvtx);

	float dist  = vertexdistance-planedistance;

	vec4 clip_pos = PROJECTION_MATRIX * vec4(VERTEX,1.0);
	vec2 uv = clip_pos.xy;
	uv *= 0.2; // lower frequency of noise sampling
	uv += TIME*0.03;
	float noise = texture(_noise,uv).r;
	dist += noise*0.3;

	if (dist < 0.0)
		discard;

	float borderdist = smoothstep(0.0,0.3,dist);
	vec4 alb_col = texture(_albedo,UV);

	ALBEDO = mix(border_colour, alb_col, borderdist).rgb;
	EMISSION = (mix(border_colour*7.0, vec4(0.0), borderdist)).rgb;
}
