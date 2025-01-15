shader_type spatial;
uniform sampler2DArray _albedo : filter_nearest;

uniform sampler2D _displacement : filter_nearest;
uniform sampler2D _roughness : filter_nearest;
uniform sampler2D _normalmap : filter_nearest;
uniform sampler2D _noise;
uniform sampler2D _spot_noise;
uniform vec4 _bordercol : source_color;
uniform vec4 _emissioncol : source_color;
uniform vec3 _burncol : source_color;
uniform vec3 _acidcol : source_color;
uniform vec3 _acidedge : source_color;

uniform bool ENABLE_DEBUG = false;
uniform float debug_damage_amount : hint_range(0.0,1.0);
uniform bool debug_physical;
uniform bool debug_acid;
uniform bool debug_fire;

vec3[2] ApplyDamageShaders(vec2 uv, vec2 base_uv, float damageratio, bool physicaldamage, bool firedamage, bool aciddamage, vec3 finalcol, vec3 finalemission) {
	// acid damage modifies uvs to make them wavy
	// which is why the check for it comes first
	if (aciddamage) {
		float uvoffset = damageratio*0.05*sin(10.0*base_uv.y+5.0*TIME);
		//uv += uvoffset;
		//finalcol = mix(finalcol,finalcol+vec3(0.00,0.4,0),damageratio);
		float noisediscard = texture(_spot_noise,0.5*(uv+uvoffset)).r;
	    vec2 noisecoord = vec2(32,32);
	    //noisecoord += TIME*0.05;
	    float noise = texture(_noise,noisecoord).r;
	    noisediscard += noise*0.15;

		if (noisediscard < damageratio) {
			float noiseval = texture(_spot_noise,uv).r;
			noiseval += 0.05*sin(TIME);
			noiseval = pow(noiseval,2);
			finalcol = _acidcol*noiseval;
			finalemission *= noiseval;
		}
		else {
			float borderdist = smoothstep(damageratio,damageratio+0.1,noisediscard);

			finalcol = mix(vec4(_acidedge,1.0), vec4(finalcol, 1.0), borderdist).rgb;
			finalemission += (mix(vec4(_acidedge,1.0)*7.0, vec4(0.0), borderdist)).rgb;

			finalcol = mix(vec4(0.00,0.01,0,1.0), vec4(finalcol, 1.0), borderdist).rgb;
			finalemission += (mix(vec4(0.00,0.1,0,1.0)*7.0, vec4(0.0), borderdist)).rgb;

			noise += 0.05*sin(TIME);
			noise = pow(noise,2);
			finalcol = mix(vec4(finalcol, 1.0),vec4(finalcol + 2.0*_acidedge*noise,1.0),damageratio).rgb;
			finalemission *= noise;
		}
	}

	if (firedamage) {
		float noisediscard = texture(_spot_noise,uv).r;
	    vec2 noisecoord = vec2(0);
	    noisecoord += TIME*0.05;
	    float noise = texture(_noise,noisecoord).r;
	    noisediscard += noise*0.05;
		if (noisediscard < damageratio) {
			float noiseval = texture(_spot_noise,uv).r;
			noiseval += 0.05*sin(TIME);
			noiseval = pow(noiseval,2);
			finalcol = _burncol*noiseval;
			finalemission *= noiseval;
		}
		else {
			float borderdist = smoothstep(damageratio,damageratio+0.1,noisediscard);

			finalcol = mix(_bordercol, vec4(finalcol, 1.0), borderdist).rgb;
			finalemission += (mix(_emissioncol*7.0, vec4(0.0), borderdist)).rgb;
		}
	}

	if (physicaldamage) {
		float cracks = texture(_displacement, 0.2*uv).r;
		cracks = pow(cracks, mix(0.0,20,damageratio));
		finalcol *= cracks;
	}
	
	return {finalcol,finalemission};
}

void fragment() {
	// stored metadata in vertex colour buffer
	// texture array frame index in color.r
	// uv stretch factors in color.gb
	// the block's health and damage type is stored in color.a
	vec4 metadata = round(COLOR*255.0);
	vec2 uv = fract(UV*metadata.gb);
	vec3 coords = vec3(uv, metadata.r);
	vec4 basecol = texture(_albedo,coords);

	// calculate damages
	int damagedata = int(metadata.a);
	float damageratio = float(damagedata&0x1f)/31.0;
	bool physicaldamage = damageratio > 0.0 && (damagedata>>5 & 1) == 1;
	bool firedamage = damageratio > 0.0 && (damagedata>>6 & 1) == 1;
	bool aciddamage = damageratio > 0.0 && (damagedata>>7 & 1) == 1;
	if (ENABLE_DEBUG) {
		damageratio = debug_damage_amount;
		firedamage = damageratio > 0.0 && debug_fire;
		aciddamage = damageratio > 0.0 && debug_acid;
		physicaldamage = damageratio > 0.0 && debug_physical;
	}
	bool notDamaged = (!(physicaldamage || firedamage || aciddamage) || (damageratio < 0.001));

	vec3 finalcol = basecol.rgb;
	vec3 finalemission = vec3(0);
	
	if (notDamaged) ALBEDO = finalcol;
	else
	{
		vec3[2] res = ApplyDamageShaders(uv, UV, damageratio, physicaldamage, firedamage, aciddamage, finalcol, finalemission);
		
		finalcol = res[0];
		finalemission = res[1];
		
		float normscale = mix(0.0,3.0,damageratio);
		if (aciddamage || physicaldamage)
		{
			NORMAL_MAP = texture(_normalmap, 0.2*uv).rgb;
			NORMAL_MAP_DEPTH = normscale;
		}
		if (aciddamage)
		{
			SPECULAR = mix(0.0,1.0,damageratio);
			ROUGHNESS = texture(_roughness, 0.2*uv).r * normscale;	
		}
		
		ALBEDO = finalcol;
		
		
		EMISSION = finalemission;
	}
}