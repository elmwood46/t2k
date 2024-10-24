shader_type spatial;
render_mode unshaded,cull_disabled;

uniform sampler2D texture_albedo :source_color, filter_linear; // The texture to apply
uniform vec4 outline_color : source_color = vec4(1.0, 0.0, 0.0, 1.0); // Outline color
uniform float outline_width = 2.0;
uniform bool constant_width = true;
uniform bool y_billboard = false; // Enables Y-Billboarding when true

void vertex() {
	// You can comment out this function if you don't need billboarding
	if (y_billboard == true) {
		MODELVIEW_MATRIX = VIEW_MATRIX * mat4(vec4(normalize(cross(vec3(0.0, 1.0, 0.0), INV_VIEW_MATRIX[2].xyz)), 0.0), vec4(0.0, 1.0, 0.0, 0.0), vec4(normalize(cross(INV_VIEW_MATRIX[0].xyz, vec3(0.0, 1.0, 0.0))), 0.0), MODEL_MATRIX[3]);
	}
	else {
		MODELVIEW_MATRIX = VIEW_MATRIX * mat4(INV_VIEW_MATRIX[0], INV_VIEW_MATRIX[1], INV_VIEW_MATRIX[2], MODEL_MATRIX[3]);
	}
	MODELVIEW_NORMAL_MATRIX = mat3(MODELVIEW_MATRIX);
}
void fragment() {
	vec4 pixelatedtext = texture(texture_albedo, UV);
	vec2 pixel_size = 1.0 / VIEWPORT_SIZE;
	ALBEDO = pixelatedtext.rgb;
	for(int y = -1*int(outline_width); y <= 1*int(outline_width); y++)
	for(int x = -1*int(outline_width); x <= 1*int(outline_width); x++)
	{
		vec4 tex = texture(texture_albedo, UV + vec2(float(x),float(y)) * pixel_size );
		if(tex.a == 0.0 && UV.x + float(x) * pixel_size.x < 1.0 && UV.x + float(x) * pixel_size.x > 0.0 && SCREEN_UV.y + float(y) * pixel_size.y < 1.0 && SCREEN_UV.y + float(y) * pixel_size.y > 0.0)
		{
			ALBEDO = outline_color.rgb;
			ALPHA = 1.0;
		}
	}
}