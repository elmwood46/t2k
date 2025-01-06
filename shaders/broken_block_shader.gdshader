shader_type spatial;
render_mode ensure_correct_normals;
uniform sampler2DArray albedo_texture : filter_nearest;

uniform float subdivs;

// this is the shader for a mini block after
// a "parent" block has been subdivided
// the boxmesh has a uv layout of 3x2
// this shader is for a boxmesh only, so we 
// assume there is a boxmesh UV layout

// which side of the parent block this mini block was on
// 0-6 bottom, top, left, right, back, front
// -1 means it's internal, and will be textured
// with the block's bottom texture
uniform int parent_face;

// 9 slice, indexed 0-8
// 0-2 = bottom, 3-5 = middle, 6-8 = top
// 5 means centre block
// 0,0 is lower left
// 1 is lower middle
// 2 is lower right and so on
uniform int nine_slice;

// holds the texture array positions
// of the parent block (for looking up the sampler2d)
uniform int[6] tex_array_idx;

// check vector equality
bool equals(vec3 v1, vec3 v2) {
	return abs(dot(v1,v2) - 1.0) < 0.001;
}

void fragment() {
	//vec3 normal = NORMAL;
	// assume boxmesh has 3x2 uv layout
	float facex = min(UV.x,0.999999f)*3.0;
	float facey = min(UV.y,0.999999f)*2.0;
	
	// get idx of uv wrapped around boxmesh
	int idx = int(floor(facex) + floor(facey)*3.0);
	// transform the boxmesh index to the indexing style of our blocks
	// (looking from +y, boxmesh starts at +z and wraps around anticlockwise (so +z, +x, -z, -x) before doing top then bottom)
	// (our blocks go bottom,top,left,right,back,front)
	switch (idx) {
		case 0: idx = 5; break;
		case 1: idx = 3; break;
		case 2: idx = 4; break;
		case 3: idx = 2; break;
		case 4: idx = 1; break;
		case 5: default : idx = 0; break;
	};
	
	float texture_frame = float(tex_array_idx[idx]);
	
	/*
	vec2 uv_base = vec2(fract(facex),fract(facey));
	vec2 uvmod = uv_base * 1.0/subdivs;
	if (parent_face == 4) {
		if (idx == 4) {
			
		}
	}
	*/
	
	vec3 coord = vec3(fract(facex),fract(facey),texture_frame);
	ALBEDO = texture(albedo_texture,coord).rgb;
}

//void light() {
	// Called for every pixel for every light affecting the material.
	// Uncomment to replace the default light processing function with this one.
//}
