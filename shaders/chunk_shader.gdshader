shader_type spatial;
render_mode ensure_correct_normals;
uniform sampler2D albedo_texture : repeat_enable, filter_nearest;

// change this depending on vox size
const float inv_voxel_size = 2.0f;

varying vec3 local_vert;
varying vec3 local_norm;


void vertex() {
	local_vert = VERTEX;
	local_norm = NORMAL;
}

bool equals(vec3 v1, vec3 v2) {
	return abs(dot(v1,v2) - 1.0f) < 0.001;
}

void fragment() {
	vec3 normal = local_norm;
	vec3 position = local_vert;
	int face = 0;
	// we are shading voxels
	if (equals(normal,vec3(0,-1,0))) {face = 0;}
	else if (equals(normal,vec3(0,1,0))) {face = 1;}
	else if (equals(normal,vec3(-1,0,0))) {face = 2;}
	else if (equals(normal,vec3(1,0,0))) {face = 3;}
	else if (equals(normal,vec3(0,-1,0))) {face = 4;}
	else {face = 5;}
	
	vec2 sample_uv;
	if (face == 0 || face == 1) {
		sample_uv = position.xz;
	}
	else if (face == 2 || face == 3) {
		sample_uv = position.zy;
	}
	else sample_uv = position.xy;

	//sample_uv *= inv_voxel_size;
	sample_uv = fract(sample_uv);
	
	ALBEDO = texture(albedo_texture,sample_uv).rgb;
}

//void light() {
	// Called for every pixel for every light affecting the material.
	// Uncomment to replace the default light processing function with this one.
//}
