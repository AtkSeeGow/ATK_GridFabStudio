// GridFabStudio lightweight Gridfinity-compatible baseplate.
// Independent implementation. Dimensions are in millimeters.

size_mode = 0;          // 0: whole 42 mm grid cells, 1: exact outside millimeters
cells_x = 3;
cells_y = 3;
width_mm = 126;
depth_mm = 126;
drawer_clearance = 0.4; // removed from both sides of each axis in millimeter mode
magnet_mode = 0;        // 0: off, 1: all four corners, 2: diagonal pair
magnet_diameter = 6.2;
magnet_depth = 2.2;
junction_holes = true;

pitch = 42;
socket_height = 5;
magnet_floor = 0.6;
edge_wall = 1.2;
epsilon = 0.02;
// 24 segments keeps magnet holes round enough for printing while avoiding
// excessive CGAL work on large plates with many repeated cutters.
$fn = 24;

plate_width = size_mode == 0 ? cells_x * pitch : width_mm - 2 * drawer_clearance;
plate_depth = size_mode == 0 ? cells_y * pitch : depth_mm - 2 * drawer_clearance;
columns = ceil((plate_width - 0.001) / pitch);
rows = ceil((plate_depth - 0.001) / pitch);
base_height = magnet_mode == 0 ? 0 : magnet_depth + magnet_floor;
plate_height = base_height + socket_height;
partial_x = abs(plate_width / pitch - round(plate_width / pitch)) > 0.001;
partial_y = abs(plate_depth / pitch - round(plate_depth / pitch)) > 0.001;

module rounded_rect(width, depth, radius) {
    offset(r = radius)
        square([width - 2 * radius, depth - 2 * radius], center = true);
}

module footprint(height) {
    linear_extrude(height = height)
        rounded_rect(plate_width, plate_depth,
            min(4, plate_width / 2 - epsilon, plate_depth / 2 - epsilon));
}

module socket_section(z, size, radius) {
    translate([0, 0, z])
        linear_extrude(height = epsilon)
            rounded_rect(size, size, radius);
}

module socket() {
    // Matches the staged foot profile used by GridFabStudio bins with print clearance.
    hull() {
        socket_section(base_height - epsilon, 36.0, 1.0);
        socket_section(base_height + 0.9, 37.6, 1.8);
    }
    hull() {
        socket_section(base_height + 0.9, 37.6, 1.8);
        socket_section(base_height + 2.8, 37.6, 1.8);
    }
    hull() {
        socket_section(base_height + 2.8, 37.6, 1.8);
        socket_section(plate_height + epsilon, 41.8, 3.9);
    }
}

module all_sockets() {
    for (ix = [0 : columns - 1], iy = [0 : rows - 1])
        translate([
            -plate_width / 2 + pitch / 2 + ix * pitch,
            -plate_depth / 2 + pitch / 2 + iy * pitch,
            0
        ]) render(convexity = 4) socket();
}

function cell_fits(ix, iy) =
    (ix + 1) * pitch <= plate_width + epsilon &&
    (iy + 1) * pitch <= plate_depth + epsilon;

module magnet_pockets() {
    if (magnet_mode != 0)
        for (ix = [0 : columns - 1], iy = [0 : rows - 1])
            if (cell_fits(ix, iy))
                for (sx = [-1, 1], sy = [-1, 1])
                    if (magnet_mode == 1 || sx == sy)
                        translate([
                            -plate_width / 2 + pitch / 2 + ix * pitch + sx * 13,
                            -plate_depth / 2 + pitch / 2 + iy * pitch + sy * 13,
                            base_height - magnet_depth
                        ])
                            cylinder(h = magnet_depth + epsilon, d = magnet_diameter);
}

module crossing_holes() {
    if (junction_holes && columns > 1 && rows > 1)
        for (ix = [1 : columns - 1], iy = [1 : rows - 1]) {
            x = -plate_width / 2 + ix * pitch;
            y = -plate_depth / 2 + iy * pitch;
            if (x > -plate_width / 2 + 2 && x < plate_width / 2 - 2 &&
                y > -plate_depth / 2 + 2 && y < plate_depth / 2 - 2)
                translate([x - 1, y - 1, -epsilon])
                    cube([2, 2, plate_height + 2 * epsilon]);
        }
}

module partial_edge_caps() {
    if (size_mode == 1 && (partial_x || partial_y))
        intersection() {
            footprint(plate_height);
            union() {
                if (partial_x)
                    translate([plate_width / 2 - edge_wall, -plate_depth / 2, 0])
                        cube([edge_wall, plate_depth, plate_height]);
                if (partial_y)
                    translate([-plate_width / 2, plate_depth / 2 - edge_wall, 0])
                        cube([plate_width, edge_wall, plate_height]);
            }
        }
}

assert(plate_width >= 10 && plate_depth >= 10, "Outside dimensions must be at least 10 mm.");
assert(columns * rows <= 36, "The first version supports at most 36 cells.");
assert(magnet_mode == 0 || magnet_depth + magnet_floor <= base_height + epsilon,
    "Magnet pocket leaves no floor.");

union() {
    difference() {
        footprint(plate_height);
        all_sockets();
        magnet_pockets();
        crossing_holes();
    }
    partial_edge_caps();
}
