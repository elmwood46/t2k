shader_type spatial;
uniform sampler2DArray _albedo : filter_nearest;

void fragment() {
	// stored metadata in vertex colour buffer
	// texture array frame index in color.r
	// uv stretch factors in color.gb
	vec4 col = COLOR*255.0;
	vec3 coords = vec3(fract(UV*col.gb), col.r);
	vec4 basecol = texture(_albedo,coords);

	ALBEDO = basecol.rgb;
}