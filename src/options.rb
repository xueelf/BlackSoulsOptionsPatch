
module BSOPTS
  RESOLUTIONS = [
    "640x480",
    "1280x960",
    "1920x1440",
    "2560x1920"
  ]

  DEFAULTS = {
    :resolution => "640x480",
    :fullscreen => false,
    :bgm        => 100,
    :bgs        => 100,
    :me         => 100,
    :se         => 100
  }

  SWP_NOZORDER     = 0x0004
  SWP_NOACTIVATE   = 0x0010
  SWP_FRAMECHANGED = 0x0020
  SWP_SHOWWINDOW   = 0x0040
  GWL_STYLE        = -16
  GWL_EXSTYLE      = -20
  WS_POPUP         = 0x80000000
  WS_VISIBLE       = 0x10000000
  SPI_GETWORKAREA  = 0x0030
  SW_RESTORE       = 9
  GW_HWNDNEXT      = 2

  @config = nil
  @work_area = nil
  @window_handle = 0
  @fullscreen_saved = nil
  @return_to_title_options = false

  #--------------------------------------------------------------------------
  # Win32 helpers
  #--------------------------------------------------------------------------
  module BSWin32
    GET_ACTIVE_WINDOW              = Win32API.new("user32", "GetActiveWindow", "", "I")
    GET_FOREGROUND_WINDOW          = Win32API.new("user32", "GetForegroundWindow", "", "I")
    GET_TOP_WINDOW                 = Win32API.new("user32", "GetTopWindow", "I", "I")
    GET_WINDOW                     = Win32API.new("user32", "GetWindow", "II", "I")
    GET_WINDOW_THREAD_PROCESS_ID   = Win32API.new("user32", "GetWindowThreadProcessId", "IP", "I")
    GET_CURRENT_PROCESS_ID         = Win32API.new("kernel32", "GetCurrentProcessId", "", "I")
    GET_CLASS_NAME                 = Win32API.new("user32", "GetClassNameA", "IPI", "I")
    GET_WINDOW_RECT                = Win32API.new("user32", "GetWindowRect", "IP", "I")
    GET_CLIENT_RECT                = Win32API.new("user32", "GetClientRect", "IP", "I")
    GET_WINDOW_LONG                = Win32API.new("user32", "GetWindowLongA", "II", "I")
    SET_WINDOW_LONG                = Win32API.new("user32", "SetWindowLongA", "III", "I")
    SET_WINDOW_POS                 = Win32API.new("user32", "SetWindowPos", "IIIIIII", "I")
    SHOW_WINDOW                    = Win32API.new("user32", "ShowWindow", "II", "I")
    GET_SYSTEM_METRICS             = Win32API.new("user32", "GetSystemMetrics", "I", "I")
    SYSTEM_PARAMETERS_INFO         = Win32API.new("user32", "SystemParametersInfoA", "ILPI", "I")
  end

  class << self
    def config
      @config ||= load_config
    end

    def load_config
      cfg = DEFAULTS.dup

      begin
        if File.exist?(config_path)
          File.open(config_path, "rb") do |file|
            file.each_line do |line|
              line = line.strip
              next if line.empty? || line[0, 1] == "#"

              key, value = line.split("=", 2)
              next if key.nil? || value.nil?

              case key.strip
              when "resolution"
                cfg[:resolution] = value.strip
              when "fullscreen"
                cfg[:fullscreen] = (value.to_i != 0)
              when "bgm"
                cfg[:bgm] = clamp_volume(value.to_i)
              when "bgs"
                cfg[:bgs] = clamp_volume(value.to_i)
              when "me"
                cfg[:me] = clamp_volume(value.to_i)
              when "se"
                cfg[:se] = clamp_volume(value.to_i)
              end
            end
          end
        end
      rescue
      end

      cfg[:resolution] = "640x480" unless RESOLUTIONS.include?(cfg[:resolution])
      cfg
    end

    def save_config
      begin
        File.open(config_path, "wb") do |file|
          file.write("# BLACK SOULS options\n")
          file.write("# This file is re-created automatically.\n")
          file.write("resolution=#{config[:resolution]}\n")
          file.write("fullscreen=#{config[:fullscreen] ? 1 : 0}\n")
          file.write("bgm=#{config[:bgm]}\n")
          file.write("bgs=#{config[:bgs]}\n")
          file.write("me=#{config[:me]}\n")
          file.write("se=#{config[:se]}\n")
        end
      rescue
      end
    end

    def config_path
      File.join(Dir.pwd, "Options.ini")
    end

    def clamp_volume(value)
      value = 0 if value < 0
      value = 100 if value > 100
      value
    end

    def scale_volume(base, key)
      value = (base * config[key] / 100.0).round
      clamp_volume(value)
    end

    #------------------------------------------------------------------------
    # Window size
    #------------------------------------------------------------------------
    def work_area
      return @work_area if @work_area

      begin
        buffer = [0, 0, 0, 0].pack("l4")
        if BSWin32::SYSTEM_PARAMETERS_INFO.call(SPI_GETWORKAREA, 0, buffer, 0) != 0
          @work_area = buffer.unpack("l4")
        else
          @work_area = [0, 0, BSWin32::GET_SYSTEM_METRICS.call(0),
                        BSWin32::GET_SYSTEM_METRICS.call(1)]
        end
      rescue
        @work_area = [0, 0, 0, 0]
      end

      @work_area
    end

    def resolution_label
      config[:resolution].sub("x", " x ")
    end

    def cycle_resolution(direction)
      index = RESOLUTIONS.index(config[:resolution]) || 0
      index = (index + direction) % RESOLUTIONS.length
      config[:resolution] = RESOLUTIONS[index]
      save_config
      apply_window
    end

    def toggle_fullscreen
      config[:fullscreen] = !config[:fullscreen]
      save_config
      apply_window
    end

    def change_volume(key, delta)
      old_value = config[key]
      new_value = clamp_volume(old_value + delta)
      return false if new_value == old_value

      config[key] = new_value
      save_config
      Audio.bs_options_replay_bgm if key == :bgm
      Audio.bs_options_replay_bgs if key == :bgs
      true
    end

    def reset
      @config = DEFAULTS.dup
      save_config
      apply_window
      Audio.bs_options_replay_bgm
      Audio.bs_options_replay_bgs
    end

    #------------------------------------------------------------------------
    # Win32 window handling
    #------------------------------------------------------------------------
    def process_window?(handle)
      return false if handle == 0

      buffer = [0].pack("L")
      BSWin32::GET_WINDOW_THREAD_PROCESS_ID.call(handle, buffer)
      pid = buffer.unpack("L")[0]
      return false if pid != BSWin32::GET_CURRENT_PROCESS_ID.call

      name = "\0" * 256
      length = BSWin32::GET_CLASS_NAME.call(handle, name, 256)

      name[0, length] == "RGSS Player"
    rescue
      false
    end

    def find_window
      return @window_handle if @window_handle != 0 && process_window?(@window_handle)

      [BSWin32::GET_ACTIVE_WINDOW.call, BSWin32::GET_FOREGROUND_WINDOW.call].each do |handle|
        if process_window?(handle)
          @window_handle = handle
          return handle
        end
      end

      handle = BSWin32::GET_TOP_WINDOW.call(0)
      while handle != 0
        if process_window?(handle)
          @window_handle = handle
          return handle
        end
        handle = BSWin32::GET_WINDOW.call(handle, GW_HWNDNEXT)
      end

      0
    rescue
      0
    end

    def get_rect(api, handle)
      buffer = [0, 0, 0, 0].pack("l4")
      return nil if api.call(handle, buffer) == 0

      buffer.unpack("l4")
    rescue
      nil
    end

    def set_client_size(handle, client_width, client_height)
      BSWin32::SHOW_WINDOW.call(handle, SW_RESTORE)
      window = get_rect(BSWin32::GET_WINDOW_RECT, handle)
      client = get_rect(BSWin32::GET_CLIENT_RECT, handle)
      return false if window.nil? || client.nil?

      border_width = (window[2] - window[0]) - client[2]
      border_height = (window[3] - window[1]) - client[3]
      area = work_area
      window_width = client_width + border_width
      window_height = client_height + border_height
      x = area[0] + (area[2] - area[0] - window_width) / 2
      y = area[1] + (area[3] - area[1] - window_height) / 2
      x = area[0] if x < area[0]
      y = area[1] if y < area[1]
      BSWin32::SET_WINDOW_POS.call(handle, 0, x, y, window_width, window_height,
                                   SWP_NOZORDER | SWP_NOACTIVATE) != 0
    rescue
      false
    end

    def apply_fullscreen(handle)
      if @fullscreen_saved.nil?
        rect = get_rect(BSWin32::GET_WINDOW_RECT, handle)
        return false if rect.nil?

        style = BSWin32::GET_WINDOW_LONG.call(handle, GWL_STYLE)
        exstyle = BSWin32::GET_WINDOW_LONG.call(handle, GWL_EXSTYLE)
        @fullscreen_saved = [style, exstyle, rect]
      end
      BSWin32::SET_WINDOW_LONG.call(handle, GWL_STYLE, WS_POPUP | WS_VISIBLE)
      BSWin32::SET_WINDOW_LONG.call(handle, GWL_EXSTYLE, 0)
      width = BSWin32::GET_SYSTEM_METRICS.call(0)
      height = BSWin32::GET_SYSTEM_METRICS.call(1)
      BSWin32::SET_WINDOW_POS.call(handle, 0, 0, 0, width, height,
                                   SWP_NOZORDER | SWP_FRAMECHANGED | SWP_SHOWWINDOW) != 0
    rescue
      false
    end

    def restore_from_fullscreen(handle)
      return true if @fullscreen_saved.nil?

      style, exstyle, rect = @fullscreen_saved
      BSWin32::SET_WINDOW_LONG.call(handle, GWL_STYLE, style)
      BSWin32::SET_WINDOW_LONG.call(handle, GWL_EXSTYLE, exstyle)
      result = BSWin32::SET_WINDOW_POS.call(handle, 0, rect[0], rect[1],
                                            rect[2] - rect[0], rect[3] - rect[1],
                                            SWP_NOZORDER | SWP_FRAMECHANGED) != 0
      @fullscreen_saved = nil
      result
    rescue
      false
    end

    def apply_window
      handle = find_window
      return false if handle == 0

      if config[:fullscreen]
        apply_fullscreen(handle)
      else
        restore_from_fullscreen(handle)
        width, height = config[:resolution].split("x").map(&:to_i)
        set_client_size(handle, width, height)
      end
    rescue
      false
    end

    def start_settings(font = nil)
      @settings_font = duplicate_font(font)
      SceneManager.call(Scene_BSOptions)
    end

    def duplicate_font(font)
      return nil unless font

      copy = Font.new
      copy.name = font.name
      copy.size = font.size
      copy.bold = font.bold
      copy.italic = font.italic
      copy.outline = font.outline
      copy.shadow = font.shadow
      copy
    end

    attr_reader :settings_font
    attr_accessor :return_to_title_options
  end
end

#==============================================================================
# Audio volume hooks
#==============================================================================
module Audio
  class << self
    unless method_defined?(:bs_options_original_bgm_play)
      alias_method :bs_options_original_bgm_play, :bgm_play
      def bgm_play(filename, volume = 100, pitch = 100, *rest)
        @bs_options_last_bgm = [filename, volume, pitch, rest]
        @bs_options_bgm_playing = true

        bs_options_original_bgm_play(filename, BSOPTS.scale_volume(volume, :bgm), pitch, *rest)
      end
    end

    unless method_defined?(:bs_options_original_bgm_stop)
      alias_method :bs_options_original_bgm_stop, :bgm_stop
      def bgm_stop
        @bs_options_bgm_playing = false

        bs_options_original_bgm_stop
      end
    end

    unless method_defined?(:bs_options_original_bgs_play)
      alias_method :bs_options_original_bgs_play, :bgs_play
      def bgs_play(filename, volume = 100, pitch = 100, *rest)
        @bs_options_last_bgs = [filename, volume, pitch, rest]
        @bs_options_bgs_playing = true

        bs_options_original_bgs_play(filename, BSOPTS.scale_volume(volume, :bgs), pitch, *rest)
      end
    end

    unless method_defined?(:bs_options_original_bgs_stop)
      alias_method :bs_options_original_bgs_stop, :bgs_stop
      def bgs_stop
        @bs_options_bgs_playing = false

        bs_options_original_bgs_stop
      end
    end

    unless method_defined?(:bs_options_original_me_play)
      alias_method :bs_options_original_me_play, :me_play
      def me_play(filename, volume = 100, pitch = 100, *rest)
        bs_options_original_me_play(filename, BSOPTS.scale_volume(volume, :me), pitch, *rest)
      end
    end

    unless method_defined?(:bs_options_original_se_play)
      alias_method :bs_options_original_se_play, :se_play
      def se_play(filename, volume = 100, pitch = 100, *rest)
        bs_options_original_se_play(filename, BSOPTS.scale_volume(volume, :se), pitch, *rest)
      end
    end

    def bs_options_replay_bgm
      return unless @bs_options_bgm_playing && @bs_options_last_bgm

      filename, volume, pitch, rest = @bs_options_last_bgm

      bs_options_original_bgm_play(filename, BSOPTS.scale_volume(volume, :bgm), pitch, *rest)
    rescue
    end

    def bs_options_replay_bgs
      return unless @bs_options_bgs_playing && @bs_options_last_bgs

      filename, volume, pitch, rest = @bs_options_last_bgs

      bs_options_original_bgs_play(filename, BSOPTS.scale_volume(volume, :bgs), pitch, *rest)
    rescue
    end
  end
end

#==============================================================================
# Options window
#==============================================================================
class Window_BSOptions < Window_Command
  COMMAND_KEYS = [
    "options.display.resolution",
    "options.display.mode",
    "options.audio.bgm",
    "options.audio.bgs",
    "options.audio.me",
    "options.audio.se",
    "options.reset"
  ]

  VOLUME_KEYS = [:bgm, :bgs, :me, :se]

  def initialize(x, y, font = nil)
    @custom_font = font
    @layout_ready = false

    super(x, y)
  end

  def window_width
    calculate_layout

    width = [super * 2, @window_width].max
    if width != @window_width
      @window_width = width
      contents_width = width - standard_padding * 2
      @value_width = contents_width - 4 - @label_width - 8
    end

    width
  end

  def calculate_layout
    return if @layout_ready

    bitmap = Bitmap.new(1, 1)
    if @custom_font
      bitmap.font = @custom_font
    else
      bitmap.font.name = Font.default_name
      bitmap.font.size = Font.default_size
    end

    labels = @list.map { |item| item[:name].to_s }
    values = BSOPTS::RESOLUTIONS.map { |resolution| resolution.sub("x", " x ") }
    values << BSOPTS::TEXT["options.display.mode.fullscreen"]
    values << BSOPTS::TEXT["options.display.mode.window"]
    values << "100%"

    @label_width = labels.map { |text| bitmap.text_size(text).width }.max + 4
    @value_width = values.map { |text| bitmap.text_size(text).width }.max + 4

    bitmap.dispose

    @window_width = standard_padding * 2 + 4 + @label_width + 8 + @value_width
    @layout_ready = true
  end

  def make_command_list
    COMMAND_KEYS.each_with_index do |key, index|
      add_command(BSOPTS::TEXT[key], index)
    end
  end

  def draw_item(index)
    rect = item_rect(index)

    contents.font = @custom_font if @custom_font
    change_color(normal_color)
    draw_text(rect.x + 4, rect.y, @label_width, line_height, command_name(index))
    draw_text(rect.x + 4 + @label_width + 8, rect.y,
              @value_width, line_height, value_text(index), 2)
  end

  def value_text(index)
    case index
    when 0
      BSOPTS.resolution_label
    when 1
      BSOPTS::TEXT[BSOPTS.config[:fullscreen] ? "options.display.mode.fullscreen" : "options.display.mode.window"]
    when 2..5
      "#{BSOPTS.config[VOLUME_KEYS[index - 2]]}%"
    else
      ""
    end
  end

  def process_cursor_move
    return unless cursor_movable?

    last_index = @index
    cursor_down(true) if Input.repeat?(:DOWN)
    cursor_up(true) if Input.repeat?(:UP)

    if @index <= 1
      adjust_value(1) if Input.trigger?(:RIGHT)
      adjust_value(-1) if Input.trigger?(:LEFT)
    else
      adjust_value(1) if Input.repeat?(:RIGHT)
      adjust_value(-1) if Input.repeat?(:LEFT)
    end

    Sound.play_cursor if @index != last_index
  end

  def process_ok
    if @index == COMMAND_KEYS.length - 1
      Sound.play_ok
      BSOPTS.reset
      refresh
    else
      adjust_value(1)
    end
  end

  def adjust_value(direction)
    case @index
    when 0
      BSOPTS.cycle_resolution(direction)
      Sound.play_cursor
    when 1
      BSOPTS.toggle_fullscreen
      Sound.play_cursor
    when 2..5
      Sound.play_cursor if BSOPTS.change_volume(VOLUME_KEYS[@index - 2], direction * 5)
    else
      return
    end
    refresh
  end
end

#==============================================================================
# Options scene
#==============================================================================
class Scene_BSOptions < Scene_Base
  def start
    super
    create_background
    create_window
  end

  def create_background
    @background_bitmap = SceneManager.background_bitmap
    unless @background_bitmap
      @background_bitmap = Bitmap.new(Graphics.width, Graphics.height)
      @background_bitmap.fill_rect(@background_bitmap.rect, Color.new(0, 0, 0))
      @owns_background_bitmap = true
    end

    @background_sprite = Sprite.new
    @background_sprite.bitmap = @background_bitmap
    @background_sprite.color.set(16, 16, 16, 128)
  end

  def create_window
    @window = Window_BSOptions.new(0, 0, BSOPTS.settings_font)
    @window.x = (Graphics.width - @window.width) / 2
    @window.y = (Graphics.height - @window.height) / 2
    @window.set_handler(:cancel, method(:return_scene))
  end

  def terminate
    super
    dispose_background
  end

  def dispose_background
    if @background_sprite
      @background_sprite.dispose
      @background_sprite = nil
    end

    if @owns_background_bitmap && @background_bitmap && !@background_bitmap.disposed?
      @background_bitmap.dispose
    end

    @background_bitmap = nil
  end
end

#==============================================================================
# Add the options command to the title screen and the pause menu
#==============================================================================
unless Window_TitleCommand.method_defined?(:bs_options_make_command_list)
  class Window_TitleCommand
    alias_method :bs_options_make_command_list, :make_command_list
    def make_command_list
      bs_options_make_command_list

      indent = @list.first[:name].to_s[/\A */].to_s
      @list.insert(@list.size - 1,
                   {:name => indent + BSOPTS::TEXT["options.label.title"], :symbol => :bs_options,
                    :enabled => true, :ext => nil})
    end
  end
end

class Scene_Base
  def command_bs_options
    BSOPTS.return_to_title_options = is_a?(Scene_Title)

    BSOPTS.start_settings(@command_window.contents.font)
  end
end

unless Scene_Title.method_defined?(:bs_options_create_command_window)
  class Scene_Title
    alias_method :bs_options_create_command_window, :create_command_window
    def create_command_window
      bs_options_create_command_window

      @command_window.set_handler(:bs_options, method(:command_bs_options))
    end
  end
end

unless Window_MenuCommand.method_defined?(:bs_options_make_command_list)
  class Window_MenuCommand
    alias_method :bs_options_make_command_list, :make_command_list
    def make_command_list
      bs_options_make_command_list

      index = @list.index { |item| item[:symbol] == :save }
      index = @list.size if index.nil?

      @list.insert(index,
                   {:name => BSOPTS::TEXT["options.label.menu"], :symbol => :bs_options,
                    :enabled => true, :ext => nil})
    end
  end
end

unless Scene_Menu.method_defined?(:bs_options_create_command_window)
  class Scene_Menu
    alias_method :bs_options_create_command_window, :create_command_window
    def create_command_window
      bs_options_create_command_window

      @command_window.set_handler(:bs_options, method(:command_bs_options))
    end
  end
end

#==============================================================================
# Apply the saved window size once the game window really exists
#==============================================================================
unless Scene_Title.method_defined?(:bs_options_start)
  class Scene_Title
    alias_method :bs_options_start, :start
    def start
      bs_options_start

      BSOPTS.apply_window
      if BSOPTS.return_to_title_options
        @command_window.select_symbol(:bs_options)
        BSOPTS.return_to_title_options = false
      end
    end
  end
end
