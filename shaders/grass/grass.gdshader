shader_type particles;
#include "math.gdshaderinc"

// the position in chunk space that we wrap grass noise texture
// chunks are 32x32, so set to numbers 2,4,8, or 16
#define NOISE_CHUNK_WRAP 8.0

uniform float num_particles;
uniform float wind_angle:hint_range(0.0, 360.0, 0.01);
uniform float wind_speed:hint_range(0.0, 1.0, 0.01);
uniform float wind_strength:hint_range(0.0, 1.0, 0.01);
uniform sampler2D _noise;
uniform sampler2D _noisecolor;
uniform int num_grass_blocks;

// 32x32x32 divided by 2 is max number of grass blocks we need to populate with grass
uniform int[2048] grass_positions;
uniform int[683] grass_indices;

void start() {
}
void process() {
		// empty transform
	mat4 basis = mat4(vec4(1,0,0,0),vec4(0,1,0,0),vec4(0,0,1,0),vec4(0,0,0,1));

	int grass_blades_per_block = int(round(num_particles))/num_grass_blocks;
	int block_idx = int(INDEX)/grass_blades_per_block;

	int block_vert_idx = grass_indices[block_idx];

	vec3 blockpos = vec3 (
		float(grass_positions[block_vert_idx]),
		float(grass_positions[block_vert_idx+1]),
		float(grass_positions[block_vert_idx+2])
	);

	// get the cols and rows for the current block
	float sq_particles = sqrt(float(grass_blades_per_block));
	float sq_side = round(sq_particles);
	float localind = float(int(INDEX)-block_idx*grass_blades_per_block);
	float col_idx = mod(localind,sq_side);
	float row_idx = trunc(localind/sq_side);

	float randvalue = rand(vec2(col_idx,row_idx));
	// remap to 0.3-1.0 so I don't get 0
	randvalue = remap(randvalue,0.0,1.0,0.3,1.0);

	// define the space between grass blades
	float grass_space = 1.0/sq_side;
	// assign the position + space + randomspace
	basis[3].x = blockpos.x + grass_space * col_idx + grass_space/2.0 * (randvalue-0.5);
	basis[3].z = blockpos.z + grass_space * row_idx + grass_space/2.0 * (randvalue-0.5);
	basis[3].y = blockpos.y;

	// custom coordinates so I can load noise textures
	vec2 uv = vec2(basis[3].x/NOISE_CHUNK_WRAP,basis[3].z/NOISE_CHUNK_WRAP);

	// rotate wind_dir in the Y axis
	vec3 wind_dir = mat3(rotation3d(vec3(0,1,0),radians(wind_angle))) * vec3(1,0,0);
	// wind texture displaced by wind direction * wind speed
	float wind = texture(_noise,uv + wind_dir.xz * TIME * wind_speed).r;

	// rotate by random value (STATIC)
	float rot_y = TAU * randvalue;
	// sine wave rotation in X and Y axis
	float rot_x = sin(TIME+TAU/4.0*randvalue)/2.0 * radians(12.0*randvalue) + wind*wind_strength;
	float rot_z = sin(TIME+TAU/4.0*randvalue)/2.0 * radians(12.0*randvalue) + wind*wind_strength;

	// apply the rotations to the transform
	basis*= rotation3d(vec3(1,0,0),rot_x);
	basis*= rotation3d(vec3(0,1,0),rot_y);
	basis*= rotation3d(vec3(0,0,1),rot_z);

	// legacy code don't touch
	basis[1]+= (randvalue-0.5)*0.5;

	TRANSFORM = basis;
	// apply color to each particle
	CUSTOM.x = texture(_noisecolor,uv).r;
}
