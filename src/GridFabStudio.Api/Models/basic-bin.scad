// GridFab Studio parametric Gridfinity-compatible container. Dimensions are in mm.
cells_x = 2;
cells_y = 1;
height_units = 3;
wall = 1.6;
hole_pattern = 2; // 0: none, 1: magnets, 2: magnets + screws
magnet_diameter = 6.2;
magnet_depth = 2.2;
screw_diameter = 3.0;

pitch = 42;
width = cells_x * pitch - 0.5;
depth = cells_y * pitch - 0.5;
height = 7 * height_units;
foot_height = 4.75;
floor = 1.2;
lip_height = 3.8;
lip_flat = 0.4;
epsilon = 0.02;
$fn = 40;

module rounded_rect(w, d, r) {
    offset(r = r) square([w - 2*r, d - 2*r], center = true);
}

module foot_section(z, size, radius) {
    translate([0, 0, z])
        linear_extrude(height = epsilon) rounded_rect(size, size, radius);
}

module foot() {
    hull() {
        foot_section(0, 35.6, 0.8);
        foot_section(0.8, 37.2, 1.6);
    }
    hull() {
        foot_section(0.8, 37.2, 1.6);
        foot_section(2.6, 37.2, 1.6);
    }
    hull() {
        foot_section(2.6, 37.2, 1.6);
        foot_section(foot_height, 41.5, 3.75);
    }
}

module outer_body() {
    // The upper flare forms a durable stacking rim while preserving the
    // nominal 41.5 mm-per-cell outside envelope.
    translate([0, 0, foot_height])
        linear_extrude(height = max(epsilon, height - foot_height - lip_height))
            rounded_rect(width - 1.0, depth - 1.0, 3.25);

    hull() {
        translate([0, 0, max(foot_height, height - lip_height)])
            linear_extrude(height = epsilon)
                rounded_rect(width - 1.0, depth - 1.0, 3.25);
        translate([0, 0, height - lip_flat - epsilon])
            linear_extrude(height = epsilon)
                rounded_rect(width, depth, 3.75);
    }
    translate([0, 0, height - lip_flat])
        linear_extrude(height = lip_flat)
            rounded_rect(width, depth, 3.75);
}

module inside_cavity() {
    body_inner_w = width - 1.0 - 2*wall;
    body_inner_d = depth - 1.0 - 2*wall;
    top_inner_w = width - 2*wall;
    top_inner_d = depth - 2*wall;

    translate([0, 0, foot_height + floor])
        linear_extrude(height = max(epsilon, height - lip_height - foot_height - floor + epsilon))
            rounded_rect(body_inner_w, body_inner_d, max(0.8, 3.25 - wall));
    hull() {
        translate([0, 0, max(foot_height + floor, height - lip_height)])
            linear_extrude(height = epsilon)
                rounded_rect(body_inner_w, body_inner_d, max(0.8, 3.25 - wall));
        translate([0, 0, height - lip_flat])
            linear_extrude(height = lip_flat + epsilon)
                rounded_rect(top_inner_w, top_inner_d, max(0.8, 3.75 - wall));
    }
}

module cell_mounting_holes() {
    for (dx = [-13, 13], dy = [-13, 13])
        translate([dx, dy, -epsilon]) {
            if (hole_pattern >= 1)
                cylinder(h = magnet_depth + epsilon, d = magnet_diameter);
            if (hole_pattern == 2) {
                cylinder(h = foot_height + floor + 2*epsilon, d = screw_diameter);
                // A short intermediate step avoids a long unsupported bridge
                // where the magnet pocket closes around the screw shaft.
                translate([0, 0, magnet_depth - 0.35])
                    cylinder(h = 0.35 + epsilon, d1 = magnet_diameter, d2 = screw_diameter + 1.2);
            }
        }
}

difference() {
    union() {
        outer_body();
        for (i = [0 : cells_x - 1], j = [0 : cells_y - 1])
            translate([(i - (cells_x - 1)/2)*pitch, (j - (cells_y - 1)/2)*pitch, 0])
                foot();
    }
    inside_cavity();
    if (hole_pattern > 0)
        for (i = [0 : cells_x - 1], j = [0 : cells_y - 1])
            translate([(i - (cells_x - 1)/2)*pitch, (j - (cells_y - 1)/2)*pitch, 0])
                cell_mounting_holes();
}
