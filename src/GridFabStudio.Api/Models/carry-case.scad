// Prototype fit profile for the GridFabStudio basic bin. All dimensions in mm.
cells_x = 2;
cells_y = 2;
height_units = 3;
make_lid = false;
make_clip = false;
make_handle = false;

pitch = 42;
wall = 2.4;
floor = 2.4;
recess = 4.75;
headroom = 2;
eps = 0.02;
$fn = 24;

inner_x = cells_x * pitch + 0.8;
inner_y = cells_y * pitch + 0.8;
outer_x = inner_x + 2 * wall;
outer_y = inner_y + 2 * wall;
body_height = floor + height_units * 7 + headroom;
lid_gap = 0.4;
lid_thickness = 5.6;
lid_top = body_height + lid_gap + lid_thickness;
feet_height = 2.2;
stack_x = outer_x / 2 - 10;
stack_y = outer_y / 2 - 10;
hinge_y = outer_y / 2 + 7;
hinge_z = body_height - 0.2;
hinge_radius = 3.5;
bolt_hole = 3.4;
latch_y = -outer_y / 2 - 6.5;
latch_x = outer_x / 2 - 11;
pivot_z = body_height - 12.2;
strike_z = body_height - 0.15;
pin_spacing = strike_z - pivot_z;
has_handle = cells_x >= 2 && height_units >= 3;
handle_width = min(outer_x - 55, 104);
handle_axis_y = -outer_y / 2 - 5;
handle_axis_z = body_height - 4.8;
handle_reach = min(22, handle_axis_z - 4.8);
handle_thickness = 7.2;

module rounded_rect(x, y, r) {
    offset(r = r) square([x - 2*r, y - 2*r], center = true);
}

module block(x, y, r, h) {
    linear_extrude(height = h) rounded_rect(x, y, r);
}

module socket_slice(z, size, radius) {
    translate([0, 0, z]) block(size, size, radius, eps);
}

module socket() {
    hull() {
        socket_slice(0, 36.25, 1.1);
        socket_slice(0.8, 37.85, 1.9);
    }
    hull() {
        socket_slice(0.8, 37.85, 1.9);
        socket_slice(2.6, 37.85, 1.9);
    }
    hull() {
        socket_slice(2.6, 37.85, 1.9);
        socket_slice(recess, 41.95, 4);
    }
}

module shell() {
    difference() {
        block(outer_x, outer_y, 4, body_height);
        translate([0, 0, floor + recess])
            block(inner_x, inner_y, 1.6, body_height - floor - recess + eps);
        // Relieve the rear top edge where the middle knuckle sweeps past it.
        translate([-5, outer_y / 2 - 1.2, body_height - 1.1])
            cube([10, 1.2 + eps, 1.1 + eps]);
        for (i = [0 : cells_x - 1], j = [0 : cells_y - 1])
            translate([(i - (cells_x - 1)/2)*pitch, (j - (cells_y - 1)/2)*pitch, floor])
                socket();
    }
}

module hinge_barrel(x0, x1, diameter) {
    translate([x0, hinge_y, hinge_z])
        rotate([0, 90, 0]) cylinder(h = x1 - x0, d = diameter);
}

module body_knuckle(x0, x1) {
    difference() {
        hull() {
            hinge_barrel(x0, x1, 2 * hinge_radius);
            translate([x0, outer_y / 2 - 0.8, body_height - 8])
                cube([x1 - x0, 1.3, 4.8]);
        }
        hinge_barrel(x0 - eps, x1 + eps, bolt_hole);
    }
}

module lid_knuckle() {
    difference() {
        hull() {
            hinge_barrel(-4.6, 4.6, 2 * hinge_radius);
            translate([-4.6, outer_y / 2 - 1.7, body_height + lid_gap + 0.2])
                cube([9.2, 2.1, 2.4]);
        }
        hinge_barrel(-4.6 - eps, 4.6 + eps, bolt_hole);
    }
}

module front_pin(x0, x1, z, diameter) {
    translate([x0, latch_y, z])
        rotate([0, 90, 0]) cylinder(h = x1 - x0, d = diameter);
}

module front_ear(x0, x1, axis_z, root_z, root_height) {
    difference() {
        hull() {
            front_pin(x0, x1, axis_z, 7);
            translate([x0, -outer_y / 2 - 0.5, root_z])
                cube([x1 - x0, 1.3, root_height]);
        }
        front_pin(x0 - eps, x1 + eps, axis_z, bolt_hole);
    }
}

module front_mounts(axis_z, root_z, root_height) {
    for (center = [-latch_x, latch_x]) {
        front_ear(center - 7.5, center - 4.3, axis_z, root_z, root_height);
        front_ear(center + 4.3, center + 7.5, axis_z, root_z, root_height);
    }
}

module latch_clip() {
    linear_extrude(height = 8)
        difference() {
            hull() {
                circle(r = 4);
                translate([-0.75, pin_spacing]) circle(r = 3.75);
            }
            circle(d = bolt_hole);
            translate([0, pin_spacing]) {
                circle(d = 3.5);
                // Inward-facing throat lets the clip flip forward off the strike pin.
                rotate(-10) translate([0, -1.325]) square([4.5, 2.65]);
            }
        }
}

module side_ribs() {
    for (side = [-1, 1], end = [-1, 1])
        translate([side * (outer_x / 2 - 4.8), end * (outer_y / 2 - 4.8), 0])
            difference() {
                hull() {
                    translate([0, 0, 1.4]) block(12.8, 12.8, 3.6, eps);
                    translate([0, 0, body_height - 3.2]) block(12.8, 12.8, 3.6, eps);
                    translate([0, 0, body_height - 0.9]) block(10.6, 10.6, 3.2, eps);
                }
                translate([-side * (outer_x / 2 - 4.8), -end * (outer_y / 2 - 4.8), 0])
                    block(inner_x, inner_y, 1.6, body_height);
            }
    if (cells_y >= 2)
        for (side = [-1, 1], j = [1 : cells_y - 1])
            translate([side * (outer_x / 2 - 0.5), (j - cells_y / 2) * pitch, 2])
                block(2.4, 4.4, 0.9, body_height - 5);
}

module stack_feet() {
    for (sx = [-1, 1], sy = [-1, 1])
        translate([sx * stack_x, sy * stack_y, -feet_height])
            block(10, 9, 2, feet_height + eps);
}

module handle_pivot_hole(start, length) {
    translate([start, handle_axis_y, handle_axis_z])
        rotate([0, 90, 0]) cylinder(h = length, d = bolt_hole);
}

module handle_mounts() {
    for (side = [-1, 1]) {
        start = side > 0 ? handle_width / 2 + 0.5 : -handle_width / 2 - 4.9;
        difference() {
            hull() {
                translate([start, handle_axis_y, handle_axis_z])
                    rotate([0, 90, 0]) cylinder(h = 4.4, r = 3.7);
                translate([start, -outer_y / 2 - 0.45, handle_axis_z - 3])
                    cube([4.4, 1.3, 6]);
            }
            handle_pivot_hole(start - eps, 4.4 + 2 * eps);
        }
    }
}

module carry_handle() {
    arm = handle_width / 2 - 3.5;
    difference() {
        linear_extrude(height = handle_thickness)
            union() {
                for (side = [-1, 1])
                    hull() {
                        translate([side * arm, 0]) circle(r = 3.5);
                        translate([side * arm, handle_reach]) circle(r = 3.5);
                    }
                hull()
                    for (side = [-1, 1])
                        translate([side * arm, handle_reach]) circle(r = 3.5);
            }
        for (side = [-1, 1])
            translate([side * arm - 4, 0, handle_thickness / 2])
                rotate([0, 90, 0]) cylinder(h = 8, d = bolt_hole);
    }
}

module body() {
    union() {
        shell();
        body_knuckle(-15, -5);
        body_knuckle(5, 15);
        front_mounts(pivot_z, pivot_z - 3.4, 5.8);
        side_ribs();
        stack_feet();
        if (has_handle) handle_mounts();
    }
}

module assembled_lid() {
    union() {
        difference() {
            hull() {
                translate([0, 0, body_height + lid_gap])
                    block(outer_x + 0.4, outer_y + 0.4, 4.2, eps);
                translate([0, 0, body_height + lid_gap + 1.1])
                    block(outer_x + 3.2, outer_y + 3.2, 5.5, 2.6);
                translate([0, 0, lid_top - eps])
                    block(outer_x + 1.8, outer_y + 1.8, 4.8, eps);
            }
            for (sx = [-1, 1], sy = [-1, 1])
                translate([sx * stack_x, sy * stack_y, lid_top - 2.55])
                    block(10.8, 9.8, 2.4, 2.55 + eps);
        }
        front_mounts(strike_z, body_height + lid_gap + 0.2, 2.6);
        lid_knuckle();
    }
}

// Invert the lid for printing with its flat outer face on the build plate.
if (make_clip) latch_clip();
else if (make_handle && has_handle) carry_handle();
else if (make_lid) translate([0, 0, lid_top]) mirror([0, 0, 1]) assembled_lid();
else if (!make_handle) translate([0, 0, feet_height]) body();
