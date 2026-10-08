class_name TileWorldMapGridOverlay
extends Control

var columns := 10
var line_color := Color(0.055, 0.063, 0.059, 0.52)
var line_width := 1.6
var grid_visible := true
var selected_cell := Vector2i(-1, -1)
var selection_fill_color := Color(0.83, 0.71, 0.40, 0.16)
var selection_line_color := Color(0.43, 0.34, 0.20, 0.92)

func set_grid_visible(value: bool) -> void:
	grid_visible = value
	queue_redraw()

func set_selected_cell(cell: Vector2i) -> void:
	selected_cell = cell
	queue_redraw()

func _notification(what: int) -> void:
	if what == NOTIFICATION_RESIZED:
		queue_redraw()

func _draw() -> void:
	if columns <= 0 or size.x <= 0.0:
		return
	var cell_size := size.x / float(columns)
	if grid_visible:
		for column in range(columns + 1):
			var x := float(column) * cell_size
			draw_line(Vector2(x, 0), Vector2(x, size.y), line_color, line_width, true)
		var visible_rows := ceili(size.y / cell_size)
		for row in range(visible_rows + 1):
			var y := float(row) * cell_size
			draw_line(Vector2(0, y), Vector2(size.x, y), line_color, line_width, true)
	if selected_cell.x >= 0 and selected_cell.x < columns and selected_cell.y >= 0:
		var selected_rect := Rect2(Vector2(selected_cell) * cell_size, Vector2.ONE * cell_size)
		draw_rect(selected_rect, selection_fill_color, true)
		draw_rect(selected_rect, selection_line_color, false, 2.5, true)
