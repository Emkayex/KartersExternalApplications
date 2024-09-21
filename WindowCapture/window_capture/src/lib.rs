use rnet::{net, Delegate0, Delegate3, Delegate4};
use windows_capture::{
    capture::GraphicsCaptureApiHandler,
    frame::Frame,
    graphics_capture_api::InternalCaptureControl,
    settings::{ColorFormat, CursorCaptureSettings, DrawBorderSettings, Settings}, window::Window
};

rnet::root!();

type OnFrameReadyType = Delegate3<bool, usize, u32, u32>; // Passes the number of bytes written, the width, and the height (returns a bool where true stops recording)
type OnStoppedType = Delegate0<i32>;
type OnPercentagesCalculatedType = Delegate3<bool, f32, f32, f32>; // Passes the boost bar fill percentages to the C# application (returns a bool where true stops recording)
type OnSearchAreaDeterminedType = Delegate4<i32, i64, i64, i64, i64>;

const BASE_BOOST_BAR_WIDTH: f32 = 27.0; // The width of the colored area of the boost bar at 1080P (minus 1 for safety).
const BASE_BOOST_BAR_HEIGHT: f32 = 111.0; // The height of the colored area of the boost bar at 1080P (minus 1 for safety).

static AREA_SAMPLE_PERCENTAGES: &'static [f32] = &[0.1, 0.5, 0.9];

struct Capture {
    buf_ptr: isize,
    _buf_size_bytes: usize,
    on_frame_ready:OnFrameReadyType,
    on_stopped: OnStoppedType,
    on_percentages_calculated: OnPercentagesCalculatedType,
    on_search_area_determined: OnSearchAreaDeterminedType
}

fn is_red(r: u8, g: u8, b: u8, a: u8) -> bool {
    return (r == 0xF5) && (g == 0x00) && (b == 0x00);// && (a == 0xFF);
}

fn is_gray(r: u8, g: u8, b: u8, a: u8) -> bool {
    return (r == 0x5F) && (g == 0x5E) && (b == 0x5F);// && (a == 0xFF);
}

fn is_red_or_gray(r: u8, g: u8, b: u8, a: u8) -> bool {
    return is_red(r, g, b, a) || is_gray(r, g, b, a);
}

fn find_boost_meter_bounds_within_area(rgba_values: &[u8], width: isize, x_start: isize, x_end: isize, x_step: usize, y_start: isize, y_end: isize, y_step: usize) -> (isize, isize, isize, isize) {
    // Set the starting values for the bounds to coordinates beyond what is possible and in the wrong directions (negative width and height)
    // This will ensure that any pixels that are found will override the starting values
    let mut left_most = isize::max_value() / 4;
    let mut right_most = isize::min_value() / 4;
    let mut top_most = isize::max_value() / 4;
    let mut bottom_most = isize::min_value() / 4;

    for x_raw in (x_start..x_end).step_by(x_step) {
        for y_raw in (y_start..y_end).step_by(y_step) {
            // The values for each pixel are stored in the array as R, G, B, A, R, G, B, A, ...
            // Therefore, pixels must be processed as groups of four bytes
            // For the given pixel identified by (xRaw, yRaw), calculate the array index if all RGBA values were stored as 32-bit uints
            // Then multiply the index by four to get the correct index for the R byte
            let index_raw = (y_raw * width) + x_raw;
            let r_index = (index_raw * 4) as usize;
            if r_index >= rgba_values.len().try_into().unwrap() {
                continue;
            }

            let r = rgba_values[r_index];
            let g = rgba_values[r_index + 1];
            let b = rgba_values[r_index + 2];
            let a = rgba_values[r_index + 3];

            if is_red_or_gray(r, g, b, a) {
                left_most = left_most.min(x_raw);
                right_most = right_most.max(x_raw);
                top_most = top_most.min(y_raw);
                bottom_most = bottom_most.max(y_raw);
            }
        }
    }

    return (left_most, top_most, right_most, bottom_most);
}

fn find_boost_meter_screen_bounds(rgba_values: &[u8], width: isize, height: isize, ui_scale: f32) -> (isize, isize, isize, isize) {
    // Use only the lower portion of the screen from 50% width and height to the edges to reduce the number of pixels to process
    let area_left_bound = ((width as f32) * 0.5) as isize;
    let area_top_bound = ((height as f32) * 0.5) as isize;

    // Calculate the search step size based on the display size
    let sample_step_size_x = f32::floor(BASE_BOOST_BAR_WIDTH * ui_scale * 0.5) as isize;
    let sample_step_size_y = f32::floor(BASE_BOOST_BAR_HEIGHT * ui_scale * 0.5) as isize;

    // Sample every M pixels horizontally and N pixels vertically where M is less than the width of one boost bar and N is less than the height
    // This will ensure that the sampling will find at least one red or gray pixel from each boost bar
    // From there, a search can be conducted outward to find the bounds of the boost bars
    // This should significantly reduce the amount of processing necessary to find the bounds of the boost bars since only a small fraction of pixels are tested
    let (left_most, top_most, right_most, bottom_most) = find_boost_meter_bounds_within_area(rgba_values, width, area_left_bound, width, sample_step_size_x as usize, area_top_bound, height, sample_step_size_y as usize);

    // Check to make sure the width and height are valid otherwise return immediately
    if (left_most >= right_most) || (top_most >= bottom_most) {
        return (left_most, top_most, right_most, bottom_most);
    }

    // Expand the search area out by one increment value in all directions and find the bounds checking all pixels in the newly added areas
    let new_x_start = left_most - sample_step_size_x;
    let new_x_end = right_most + sample_step_size_x + 1;
    let new_y_start = top_most - sample_step_size_y;
    let new_y_end = bottom_most + sample_step_size_y + 1;

    let (new_left_most, new_top_most, new_right_most, new_bottom_most) = find_boost_meter_bounds_within_area(rgba_values, width, new_x_start, new_x_end, 1, new_y_start, new_y_end, 1);

    let left_most = left_most.min(new_left_most);
    let right_most = right_most.max(new_right_most);
    let top_most = top_most.min(new_top_most);
    let bottom_most = bottom_most.max(new_bottom_most);
    return (left_most, top_most, right_most, bottom_most);
}

fn calculate_boost_fill_amounts(rgba_values: &[u8], width: isize, height: isize, ui_scale: f32, on_search_area_determined: &OnSearchAreaDeterminedType) -> (f32, f32, f32) {
    let (left_most, top_most, right_most, bottom_most) = find_boost_meter_screen_bounds(rgba_values, width, height, ui_scale);

    // If the selected area width or height is not positive, return values of 0
    let area_width = right_most - left_most;
    let area_height = bottom_most - top_most;
    if (area_width <= 0) || (area_height <= 0) {
        return (0.0, 0.0, 0.0);
    }

    on_search_area_determined.call(left_most as i64, top_most as i64, right_most as i64, bottom_most as i64);

    // The bars can be sampled at the 10%, 50%, and 90% X-positions within the selected area to count the red and gray pixels
    // A black bar is missing where the fill bar cuts off, but that will be counted as a red pixel instead of a gray pixel
    let mut boost1: f32 = 0.0;
    let mut boost2: f32 = 0.0;
    let mut boost3: f32 = 0.0;
    for i in 0..AREA_SAMPLE_PERCENTAGES.len() {
        let percent = AREA_SAMPLE_PERCENTAGES[i];
        let x_raw = ((left_most as f32) + ((area_width as f32) * percent)) as isize;

        let mut gray_count = 0;
        let mut red_count = 0;
        for y_raw in top_most..top_most + area_height {
            // Calculate the index of the red byte for an RGBA value
            let index_raw = (y_raw * width) + x_raw;
            let r_index = (index_raw * 4) as usize;

            // Retrieve the RGBA values and check if the pixel is gray
            // Anything other than gray will be counted as red since those should be the only two colors present besides the black bar
            let r = rgba_values[r_index];
            let g = rgba_values[r_index + 1];
            let b = rgba_values[r_index + 2];
            let a = rgba_values[r_index + 3];
            if is_gray(r, g, b, a) {
                gray_count += 1;
            } else {
                red_count += 1;
            }
        }

        // Calculate the portion that's filled and store it to the appropriate array position
        let total_count = red_count + gray_count;
        let portion_filled = (red_count as f32) / (total_count as f32);
        if i == 0 {
            boost1 = portion_filled;
        } else if i == 1 {
            boost2 = portion_filled;
        } else if i == 2 {
            boost3 = portion_filled;
        }
    }

    return (boost1, boost2, boost3);
}

impl GraphicsCaptureApiHandler for Capture {
    type Flags = (isize, usize, OnFrameReadyType, OnStoppedType, OnPercentagesCalculatedType, OnSearchAreaDeterminedType);

    type Error = Box<dyn std::error::Error + Send + Sync>;

    fn new(flags: Self::Flags) -> Result<Self, Self::Error> {
        let s = Self {
            buf_ptr: flags.0,
            _buf_size_bytes: flags.1,
            on_frame_ready: flags.2,
            on_stopped: flags.3,
            on_percentages_calculated: flags.4,
            on_search_area_determined: flags.5
        };

        Ok(s)
    }

    fn on_frame_arrived(
        &mut self,
        frame: &mut Frame,
        capture_control: InternalCaptureControl,
    ) -> Result<(), Self::Error> {
        let buf_res = frame.buffer();
        if buf_res.is_ok() {
            // Get the raw RGBA values from the buffer
            let mut buf = buf_res.unwrap();
            let raw_buf = buf.as_raw_nopadding_buffer().unwrap();

            // Calculate the boost bar fill percentages and notify the calling application
            let (boost1, boost2, boost3) = calculate_boost_fill_amounts(&raw_buf, 1920, 1080, 1.0, &self.on_search_area_determined);
            self.on_percentages_calculated.call(boost1, boost2, boost3);

            // Copy them to the memory address given by the calling application
            unsafe {
                let ptr = self.buf_ptr as *mut u8;
                ptr.copy_from_nonoverlapping(raw_buf.as_ptr(), raw_buf.len());
            }

            // Let the calling application know that a new frame is ready
            // If the return value is true, recording should stop
            let should_stop = self.on_frame_ready.call(raw_buf.len(), frame.width(), frame.height());
            if should_stop {
                capture_control.stop();
            }
        }

        Ok(())
    }

    fn on_closed(&mut self) -> Result<(), Self::Error> {
        _ = self.on_stopped.call();
        Ok(())
    }
}

#[net]
pub fn start_capture(window_name: &str, buf_ptr: isize, buf_size_bytes: usize, on_frame_ready: OnFrameReadyType, on_stopped: OnStoppedType, on_percentages_calculated: OnPercentagesCalculatedType, on_search_area_determined: OnSearchAreaDeterminedType) {
    // Find the correct window, create the capture settings, and then start capturing frames on a new thread
    let window = Window::from_contains_name(&window_name).expect("No window found with the expected name.");
    let settings = Settings::new(
        window,
        CursorCaptureSettings::WithoutCursor,
        DrawBorderSettings::Default,
        ColorFormat::Rgba8,
        (buf_ptr, buf_size_bytes, on_frame_ready, on_stopped, on_percentages_calculated, on_search_area_determined)
    );
    Capture::start_free_threaded(settings).expect("Failed to capture screen.");
}
