shader_type canvas_item;
// Renders a rotating, lit Earth from an equirectangular day map + cloud map,
// onto a square sprite. Everything outside the disc is transparent.

uniform sampler2D day_tex : source_color, filter_linear_mipmap, repeat_enable;
uniform sampler2D cloud_tex : filter_linear_mipmap, repeat_enable;
uniform sampler2D night_tex : source_color, filter_linear_mipmap, repeat_enable;
uniform float spin = 0.015;
uniform vec2 light_dir = vec2(0.55, -0.35);
uniform float atmosphere = 0.9;

void fragment() {
	vec2 p = UV * 2.0 - 1.0;
	float r2 = dot(p, p);
	if (r2 > 1.0) {
		COLOR = vec4(0.0);
	} else {
		float z = sqrt(max(0.0, 1.0 - r2));
		vec3 n = vec3(p.x, -p.y, z);

		// keep the spin offset in [0,1) so texture coords never lose precision
		// after many rotations (this was the "texture breaks after a rotation" bug)
		float rot = fract(TIME * spin);
		float crot = fract(TIME * spin * 1.55);
		float baseU = atan(n.x, n.z) / (2.0 * PI) + 0.5;
		float u = baseU + rot;
		float v = acos(clamp(n.y, -1.0, 1.0)) / PI;

		// explicit LOD (rises toward the rim + the atan wrap seam) — auto-mip would
		// pick the tiniest mip at the seam and smear a band across the back
		float seam = abs(fract(baseU + 0.5) - 0.5);          // 0 at the seam
		float lod = mix(0.0, 3.5, pow(1.0 - z, 1.4)) + smoothstep(0.06, 0.0, seam) * 2.0;
		vec3 land = textureLod(day_tex, vec2(u, v), lod).rgb;
		vec3 night = textureLod(night_tex, vec2(u, v), lod).rgb;
		float ca = textureLod(cloud_tex, vec2(baseU + crot, v), lod).a;

		vec3 L = normalize(vec3(light_dir, 0.85));
		float ndl = dot(L, normalize(n));
		float lit = clamp(ndl * 1.25 + 0.12, 0.0, 1.0);

		vec3 dayside = mix(land, vec3(1.0), ca * 0.7) * lit;
		vec3 nightside = night * 1.4 * (1.0 - lit);
		vec3 col = dayside + nightside;

		// blue rim atmosphere
		float rim = pow(1.0 - z, 2.2);
		col += vec3(0.25, 0.45, 0.85) * rim * atmosphere;
		// soft terminator haze
		col += vec3(0.9, 0.6, 0.4) * smoothstep(0.0, 0.25, abs(ndl)) * (1.0 - smoothstep(0.25, 0.45, abs(ndl))) * 0.15;

		float edge = smoothstep(1.0, 0.985, r2);
		COLOR = vec4(col, edge);
	}
}
