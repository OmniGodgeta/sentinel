shader_type canvas_item;
// Renders a rotating, lit planet from a single equirectangular colour map onto a
// square sprite. Everything outside the disc is transparent.
//
// This is earth.gdshader's sphere mapping with the cloud and night-lights layers
// dropped — Earth is the only body with those maps, and it keeps its own shader. Mars,
// Europa, Titan, Triton and Pluto are one map each plus a tint and a rim colour, so the
// Shop's worlds spin the same way the home planet does instead of sitting there as flat
// sprites.

uniform sampler2D day_tex : source_color, filter_linear_mipmap, repeat_enable;
uniform float spin = 0.012;
uniform vec2 light_dir = vec2(0.55, -0.35);
// Rim/atmosphere colour and strength. Airless bodies (Europa, Triton) want this near
// zero; Titan's thick haze wants a lot of it.
uniform vec4 atmo_color : source_color = vec4(0.35, 0.5, 0.8, 1.0);
uniform float atmosphere = 0.35;
// Multiplied over the map — the Voyager/Galileo mosaics for Europa and Titan are
// greyscale radar/clear-filter products, so this is what puts their real colour back.
uniform vec4 tint : source_color = vec4(1.0, 1.0, 1.0, 1.0);
uniform float brightness = 1.0;

void fragment() {
	vec2 p = UV * 2.0 - 1.0;
	float r2 = dot(p, p);
	if (r2 > 1.0) {
		COLOR = vec4(0.0);
	} else {
		float z = sqrt(max(0.0, 1.0 - r2));
		vec3 n = vec3(p.x, -p.y, z);

		// keep the spin offset in [0,1) so texture coords never lose precision after
		// many rotations (same fix as earth.gdshader)
		float rot = fract(TIME * spin);
		float baseU = atan(n.x, n.z) / (2.0 * PI) + 0.5;
		float u = baseU + rot;
		float v = acos(clamp(n.y, -1.0, 1.0)) / PI;

		// explicit LOD, rising toward the rim and at the atan wrap seam — auto-mip picks
		// the tiniest mip at the seam and smears a band across the back of the globe
		float seam = abs(fract(baseU + 0.5) - 0.5);
		float lod = mix(0.0, 3.5, pow(1.0 - z, 1.4)) + smoothstep(0.06, 0.0, seam) * 2.0;
		vec3 surf = textureLod(day_tex, vec2(u, v), lod).rgb * tint.rgb * brightness;

		vec3 L = normalize(vec3(light_dir, 0.85));
		float ndl = dot(L, normalize(n));
		float lit = clamp(ndl * 1.25 + 0.10, 0.0, 1.0);

		vec3 col = surf * lit;
		float rim = pow(1.0 - z, 2.2);
		col += atmo_color.rgb * rim * atmosphere;

		float edge = smoothstep(1.0, 0.985, r2);
		COLOR = vec4(col, edge);
	}
}
