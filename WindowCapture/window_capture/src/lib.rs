use rnet::{net, Delegate0, Delegate3};
use windows_capture::{
    capture::GraphicsCaptureApiHandler,
    frame::Frame,
    graphics_capture_api::InternalCaptureControl,
    settings::{ColorFormat, CursorCaptureSettings, DrawBorderSettings, Settings}, window::Window
};

rnet::root!();

type OnFrameReadyType = Delegate3<bool, usize, u32, u32>; // Passes the number of bytes written, the width, and the height (returns a bool where true stops recording)
type OnStoppedType = Delegate0<i32>;

struct Capture {
    buf_ptr: isize,
    _buf_size_bytes: usize,
    on_frame_ready:OnFrameReadyType,
    on_stopped: OnStoppedType
}

impl GraphicsCaptureApiHandler for Capture {
    type Flags = (isize, usize, OnFrameReadyType, OnStoppedType);

    type Error = Box<dyn std::error::Error + Send + Sync>;

    fn new(flags: Self::Flags) -> Result<Self, Self::Error> {
        let s = Self {
            buf_ptr: flags.0,
            _buf_size_bytes: flags.1,
            on_frame_ready: flags.2,
            on_stopped: flags.3
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
            let raw_buf = buf.as_raw_buffer();

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
pub fn start_capture(window_name: &str, buf_ptr: isize, buf_size_bytes: usize, on_frame_ready: OnFrameReadyType, on_stopped: OnStoppedType) {
    // Find the correct window, create the capture settings, and then start capturing frames on a new thread
    let window = Window::from_contains_name(&window_name).expect("No window found with the expected name.");
    let settings = Settings::new(
        window,
        CursorCaptureSettings::WithoutCursor,
        DrawBorderSettings::Default,
        ColorFormat::Rgba8,
        (buf_ptr, buf_size_bytes, on_frame_ready, on_stopped)
    ).unwrap();
    Capture::start_free_threaded(settings).expect("Failed to capture screen.");
}
