shader_type canvas_item;
// Renders a rotating, lit Earth from an equirectangular day map + cloud map,
// onto a square sprite. Everything outside the disc is transparent.

uniform sampler2D day_tex : source_color, filter_linear_mipmap;
uniform sampler2D cloud_tex : filter_linear_mipmap;
uniform sampler2D night_tex : source_color, filter_linear_mipmap;
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

		float u = atan(n.x, n.z) / (2.0 * PI) + 0.5 + TIME * spin;
		float v = acos(clamp(n.y, -1.0, 1.0)) / PI;

		vec3 land = texture(day_tex, vec2(u, v)).rgb;
		vec3 night = texture(night_tex, vec2(u, v)).rgb;
		float ca = texture(cloud_tex, vec2(u + TIME * spin * 0.55, v)).a;

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
