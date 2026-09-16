using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;

namespace KukaManager.Design;

public static class Theme
{
    // Kuka 1.3: crisp Windows-first design system with restrained Apple-like spacing.
    public static readonly Color BackgroundColor = Color.FromRgb(247, 248, 250);
    public static readonly Color SurfaceColor = Colors.White;
    public static readonly Color SidebarColor = Color.FromRgb(244, 246, 249);
    public static readonly Color SidebarHoverColor = Color.FromRgb(236, 240, 245);
    public static readonly Color NavSelectedColor = Color.FromRgb(230, 241, 255);
    public static readonly Color PrimaryColor = Color.FromRgb(0, 120, 212);
    public static readonly Color PrimaryHoverColor = Color.FromRgb(0, 103, 184);
    public static readonly Color PrimaryPressedColor = Color.FromRgb(0, 90, 158);
    public static readonly Color TextColor = Color.FromRgb(25, 30, 38);
    public static readonly Color MutedColor = Color.FromRgb(92, 103, 117);
    public static readonly Color BorderColor = Color.FromRgb(221, 226, 233);
    public static readonly Color SoftBorderColor = Color.FromRgb(233, 237, 242);
    public static readonly Color DangerColor = Color.FromRgb(196, 43, 58);
    public static readonly Color SuccessColor = Color.FromRgb(16, 137, 79);
    public static readonly Color BrandOrangeColor = Color.FromRgb(255, 116, 34);

    // Chinese is deliberately first: YaHei UI has better CJK hinting at common
    // Windows scaling factors, while Segoe UI/Variable remains the Latin fallback.
    public static readonly FontFamily BodyFont = new("Microsoft YaHei UI, Segoe UI Variable Text, Segoe UI");
    public static readonly FontFamily DisplayFont = new("Segoe UI Variable Display, Microsoft YaHei UI, Segoe UI");
    public static readonly FontFamily IconFont = new("Segoe Fluent Icons, Segoe MDL2 Assets");

    public static SolidColorBrush Brush(Color color)
    {
        var b = new SolidColorBrush(color);
        b.Freeze();
        return b;
    }

    public static void Apply(Application app)
    {
        app.Resources["Kuka.Background"] = Brush(BackgroundColor);
        app.Resources["Kuka.Surface"] = Brush(SurfaceColor);
        app.Resources["Kuka.Sidebar"] = Brush(SidebarColor);
        app.Resources["Kuka.SidebarHover"] = Brush(SidebarHoverColor);
        app.Resources["Kuka.NavSelected"] = Brush(NavSelectedColor);
        app.Resources["Kuka.Primary"] = Brush(PrimaryColor);
        app.Resources["Kuka.PrimaryHover"] = Brush(PrimaryHoverColor);
        app.Resources["Kuka.PrimaryPressed"] = Brush(PrimaryPressedColor);
        app.Resources["Kuka.Text"] = Brush(TextColor);
        app.Resources["Kuka.Muted"] = Brush(MutedColor);
        app.Resources["Kuka.Border"] = Brush(BorderColor);
        app.Resources["Kuka.SoftBorder"] = Brush(SoftBorderColor);
        app.Resources["Kuka.Danger"] = Brush(DangerColor);
        app.Resources["Kuka.Success"] = Brush(SuccessColor);
        app.Resources["Kuka.BrandOrange"] = Brush(BrandOrangeColor);
        app.Resources["Kuka.BodyFont"] = BodyFont;
        app.Resources["Kuka.DisplayFont"] = DisplayFont;
        app.Resources["Kuka.IconFont"] = IconFont;

        var modern = (ResourceDictionary)XamlReader.Parse(ModernStyles);
        app.Resources.MergedDictionaries.Add(modern);
    }

    private const string ModernStyles = """
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

    <FontFamily x:Key="BodyFont">Microsoft YaHei UI, Segoe UI Variable Text, Segoe UI</FontFamily>
    <FontFamily x:Key="DisplayFont">Segoe UI Variable Display, Microsoft YaHei UI, Segoe UI</FontFamily>
    <SolidColorBrush x:Key="SurfaceBrush" Color="#FFFFFFFF"/>
    <SolidColorBrush x:Key="CanvasBrush" Color="#FFF7F8FA"/>
    <SolidColorBrush x:Key="TextBrush" Color="#FF191E26"/>
    <SolidColorBrush x:Key="MutedBrush" Color="#FF5C6775"/>
    <SolidColorBrush x:Key="BorderBrush" Color="#FFDDE2E9"/>
    <SolidColorBrush x:Key="SoftBorderBrush" Color="#FFE9EDF2"/>
    <SolidColorBrush x:Key="AccentBrush" Color="#FF0078D4"/>
    <SolidColorBrush x:Key="AccentHoverBrush" Color="#FF0067B8"/>
    <SolidColorBrush x:Key="AccentPressedBrush" Color="#FF005A9E"/>
    <SolidColorBrush x:Key="HoverBrush" Color="#FFF3F6F9"/>
    <SolidColorBrush x:Key="PressedBrush" Color="#FFEAEFF5"/>
    <SolidColorBrush x:Key="SelectionBrush" Color="#FFE6F1FF"/>

    <Style TargetType="Window">
        <Setter Property="FontFamily" Value="{StaticResource BodyFont}"/>
        <Setter Property="FontSize" Value="14"/>
        <Setter Property="Foreground" Value="{StaticResource TextBrush}"/>
        <Setter Property="Background" Value="{StaticResource CanvasBrush}"/>
        <Setter Property="UseLayoutRounding" Value="True"/>
        <Setter Property="SnapsToDevicePixels" Value="True"/>
        <Setter Property="TextOptions.TextFormattingMode" Value="Display"/>
        <Setter Property="TextOptions.TextHintingMode" Value="Fixed"/>
        <Setter Property="TextOptions.TextRenderingMode" Value="ClearType"/>
    </Style>

    <Style TargetType="TextBlock">
        <Setter Property="FontFamily" Value="{StaticResource BodyFont}"/>
        <Setter Property="FontSize" Value="14"/>
        <Setter Property="Foreground" Value="{StaticResource TextBrush}"/>
        <Setter Property="SnapsToDevicePixels" Value="True"/>
        <Setter Property="TextOptions.TextFormattingMode" Value="Display"/>
        <Setter Property="TextOptions.TextHintingMode" Value="Fixed"/>
        <Setter Property="TextOptions.TextRenderingMode" Value="ClearType"/>
    </Style>

    <ControlTemplate x:Key="ModernButtonTemplate" TargetType="Button">
        <Grid SnapsToDevicePixels="True">
            <Border x:Name="ButtonBorder"
                    Background="{TemplateBinding Background}"
                    BorderBrush="{TemplateBinding BorderBrush}"
                    BorderThickness="{TemplateBinding BorderThickness}"
                    CornerRadius="9"/>
            <Border x:Name="InteractionOverlay" Background="#00000000" CornerRadius="9"/>
            <ContentPresenter HorizontalAlignment="{TemplateBinding HorizontalContentAlignment}"
                              VerticalAlignment="{TemplateBinding VerticalContentAlignment}"
                              Margin="{TemplateBinding Padding}"
                              SnapsToDevicePixels="True"
                              RecognizesAccessKey="True"/>
        </Grid>
        <ControlTemplate.Triggers>
            <Trigger Property="IsMouseOver" Value="True">
                <Setter TargetName="InteractionOverlay" Property="Background" Value="#0C000000"/>
            </Trigger>
            <Trigger Property="IsPressed" Value="True">
                <Setter TargetName="InteractionOverlay" Property="Background" Value="#18000000"/>
            </Trigger>
            <Trigger Property="IsKeyboardFocused" Value="True">
                <Setter TargetName="ButtonBorder" Property="BorderBrush" Value="{StaticResource AccentBrush}"/>
                <Setter TargetName="ButtonBorder" Property="BorderThickness" Value="2"/>
            </Trigger>
            <Trigger Property="IsEnabled" Value="False">
                <Setter TargetName="ButtonBorder" Property="Opacity" Value="0.48"/>
                <Setter Property="Foreground" Value="#887A8593"/>
            </Trigger>
        </ControlTemplate.Triggers>
    </ControlTemplate>

    <Style TargetType="Button">
        <Setter Property="FontFamily" Value="{StaticResource BodyFont}"/>
        <Setter Property="FontSize" Value="14"/>
        <Setter Property="FontWeight" Value="SemiBold"/>
        <Setter Property="Padding" Value="16,0"/>
        <Setter Property="MinHeight" Value="42"/>
        <Setter Property="Background" Value="{StaticResource SurfaceBrush}"/>
        <Setter Property="Foreground" Value="{StaticResource TextBrush}"/>
        <Setter Property="BorderBrush" Value="{StaticResource BorderBrush}"/>
        <Setter Property="BorderThickness" Value="1"/>
        <Setter Property="HorizontalContentAlignment" Value="Center"/>
        <Setter Property="VerticalContentAlignment" Value="Center"/>
        <Setter Property="Cursor" Value="Hand"/>
        <Setter Property="FocusVisualStyle" Value="{x:Null}"/>
        <Setter Property="UseLayoutRounding" Value="True"/>
        <Setter Property="SnapsToDevicePixels" Value="True"/>
        <Setter Property="Template" Value="{StaticResource ModernButtonTemplate}"/>
    </Style>

    <Style x:Key="PrimaryButtonStyle" TargetType="Button" BasedOn="{StaticResource {x:Type Button}}">
        <Setter Property="Background" Value="{StaticResource AccentBrush}"/>
        <Setter Property="Foreground" Value="White"/>
        <Setter Property="BorderBrush" Value="{StaticResource AccentBrush}"/>
        <Setter Property="MinWidth" Value="96"/>
    </Style>

    <Style x:Key="SecondaryButtonStyle" TargetType="Button" BasedOn="{StaticResource {x:Type Button}}">
        <Setter Property="Background" Value="White"/>
        <Setter Property="Foreground" Value="{StaticResource TextBrush}"/>
        <Setter Property="BorderBrush" Value="{StaticResource BorderBrush}"/>
    </Style>

    <Style x:Key="DangerButtonStyle" TargetType="Button" BasedOn="{StaticResource {x:Type Button}}">
        <Setter Property="Background" Value="#FFFFF5F5"/>
        <Setter Property="Foreground" Value="#FFC42B3A"/>
        <Setter Property="BorderBrush" Value="#FFF1BCC2"/>
    </Style>

    <Style x:Key="IconButtonStyle" TargetType="Button" BasedOn="{StaticResource {x:Type Button}}">
        <Setter Property="MinWidth" Value="40"/>
        <Setter Property="Width" Value="40"/>
        <Setter Property="Height" Value="40"/>
        <Setter Property="Padding" Value="0"/>
        <Setter Property="Background" Value="Transparent"/>
        <Setter Property="BorderBrush" Value="Transparent"/>
    </Style>

    <Style x:Key="SegmentButtonStyle" TargetType="Button" BasedOn="{StaticResource {x:Type Button}}">
        <Setter Property="MinWidth" Value="86"/>
        <Setter Property="MinHeight" Value="36"/>
        <Setter Property="Height" Value="36"/>
        <Setter Property="Padding" Value="14,0"/>
        <Setter Property="FontWeight" Value="Normal"/>
        <Setter Property="Background" Value="Transparent"/>
        <Setter Property="BorderBrush" Value="Transparent"/>
    </Style>

    <Style x:Key="SegmentSelectedButtonStyle" TargetType="Button" BasedOn="{StaticResource SegmentButtonStyle}">
        <Setter Property="Background" Value="White"/>
        <Setter Property="Foreground" Value="{StaticResource AccentBrush}"/>
        <Setter Property="FontWeight" Value="SemiBold"/>
        <Setter Property="BorderBrush" Value="{StaticResource SoftBorderBrush}"/>
    </Style>

    <Style x:Key="NavButtonStyle" TargetType="Button" BasedOn="{StaticResource {x:Type Button}}">
        <Setter Property="Background" Value="Transparent"/>
        <Setter Property="BorderBrush" Value="Transparent"/>
        <Setter Property="Foreground" Value="{StaticResource TextBrush}"/>
        <Setter Property="FontWeight" Value="Normal"/>
        <Setter Property="Padding" Value="0"/>
        <Setter Property="MinHeight" Value="44"/>
        <Setter Property="HorizontalContentAlignment" Value="Stretch"/>
    </Style>

    <ControlTemplate x:Key="ModernTextBoxTemplate" TargetType="TextBox">
        <Border x:Name="TextBorder"
                Background="{TemplateBinding Background}"
                BorderBrush="{TemplateBinding BorderBrush}"
                BorderThickness="{TemplateBinding BorderThickness}"
                CornerRadius="8"
                SnapsToDevicePixels="True">
            <ScrollViewer x:Name="PART_ContentHost" Margin="12,8" VerticalAlignment="{TemplateBinding VerticalContentAlignment}"/>
        </Border>
        <ControlTemplate.Triggers>
            <Trigger Property="IsMouseOver" Value="True">
                <Setter TargetName="TextBorder" Property="BorderBrush" Value="#FFB8C2CF"/>
            </Trigger>
            <Trigger Property="IsKeyboardFocused" Value="True">
                <Setter TargetName="TextBorder" Property="BorderBrush" Value="{StaticResource AccentBrush}"/>
            </Trigger>
            <Trigger Property="IsEnabled" Value="False">
                <Setter TargetName="TextBorder" Property="Opacity" Value="0.55"/>
            </Trigger>
        </ControlTemplate.Triggers>
    </ControlTemplate>

    <Style TargetType="TextBox">
        <Setter Property="FontFamily" Value="{StaticResource BodyFont}"/>
        <Setter Property="FontSize" Value="14"/>
        <Setter Property="MinHeight" Value="44"/>
        <Setter Property="Padding" Value="0"/>
        <Setter Property="Foreground" Value="{StaticResource TextBrush}"/>
        <Setter Property="Background" Value="{StaticResource SurfaceBrush}"/>
        <Setter Property="BorderBrush" Value="{StaticResource BorderBrush}"/>
        <Setter Property="BorderThickness" Value="1"/>
        <Setter Property="SelectionBrush" Value="#553B82F6"/>
        <Setter Property="CaretBrush" Value="{StaticResource AccentBrush}"/>
        <Setter Property="VerticalContentAlignment" Value="Center"/>
        <Setter Property="UseLayoutRounding" Value="True"/>
        <Setter Property="SnapsToDevicePixels" Value="True"/>
        <Setter Property="TextOptions.TextFormattingMode" Value="Display"/>
        <Setter Property="TextOptions.TextHintingMode" Value="Fixed"/>
        <Setter Property="TextOptions.TextRenderingMode" Value="ClearType"/>
        <Setter Property="Template" Value="{StaticResource ModernTextBoxTemplate}"/>
    </Style>

    <Style x:Key="SearchTextBoxStyle" TargetType="TextBox" BasedOn="{StaticResource {x:Type TextBox}}">
        <Setter Property="MinHeight" Value="0"/>
        <Setter Property="Background" Value="Transparent"/>
        <Setter Property="BorderThickness" Value="0"/>
        <Setter Property="Padding" Value="0"/>
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="TextBox">
                    <ScrollViewer x:Name="PART_ContentHost" VerticalAlignment="Center"/>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <ControlTemplate x:Key="ModernPasswordBoxTemplate" TargetType="PasswordBox">
        <Border x:Name="PasswordBorder"
                Background="{TemplateBinding Background}"
                BorderBrush="{TemplateBinding BorderBrush}"
                BorderThickness="{TemplateBinding BorderThickness}"
                CornerRadius="8"
                SnapsToDevicePixels="True">
            <ScrollViewer x:Name="PART_ContentHost" Margin="12,8" VerticalAlignment="{TemplateBinding VerticalContentAlignment}"/>
        </Border>
        <ControlTemplate.Triggers>
            <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="PasswordBorder" Property="BorderBrush" Value="#FFB8C2CF"/></Trigger>
            <Trigger Property="IsKeyboardFocused" Value="True">
                <Setter TargetName="PasswordBorder" Property="BorderBrush" Value="{StaticResource AccentBrush}"/>
            </Trigger>
        </ControlTemplate.Triggers>
    </ControlTemplate>

    <Style TargetType="PasswordBox">
        <Setter Property="FontFamily" Value="{StaticResource BodyFont}"/>
        <Setter Property="FontSize" Value="14"/>
        <Setter Property="MinHeight" Value="44"/>
        <Setter Property="Padding" Value="0"/>
        <Setter Property="Foreground" Value="{StaticResource TextBrush}"/>
        <Setter Property="Background" Value="{StaticResource SurfaceBrush}"/>
        <Setter Property="BorderBrush" Value="{StaticResource BorderBrush}"/>
        <Setter Property="BorderThickness" Value="1"/>
        <Setter Property="VerticalContentAlignment" Value="Center"/>
        <Setter Property="Template" Value="{StaticResource ModernPasswordBoxTemplate}"/>
    </Style>

    <ControlTemplate x:Key="ModernComboBoxTemplate" TargetType="ComboBox">
        <Grid SnapsToDevicePixels="True">
            <Border x:Name="ComboBorder"
                    Background="{TemplateBinding Background}"
                    BorderBrush="{TemplateBinding BorderBrush}"
                    BorderThickness="{TemplateBinding BorderThickness}"
                    CornerRadius="8">
                <Grid>
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="*"/>
                        <ColumnDefinition Width="36"/>
                    </Grid.ColumnDefinitions>
                    <ContentPresenter Margin="12,0,6,0"
                                      VerticalAlignment="Center"
                                      HorizontalAlignment="Left"
                                      Content="{TemplateBinding SelectionBoxItem}"
                                      ContentTemplate="{TemplateBinding SelectionBoxItemTemplate}"
                                      ContentStringFormat="{TemplateBinding SelectionBoxItemStringFormat}"
                                      SnapsToDevicePixels="True"/>
                    <Path Grid.Column="1" Data="M 0 0 L 4 4 L 8 0" Stroke="{StaticResource MutedBrush}" StrokeThickness="1.5" StrokeStartLineCap="Round" StrokeEndLineCap="Round" HorizontalAlignment="Center" VerticalAlignment="Center"/>
                    <ToggleButton Grid.ColumnSpan="2" Focusable="False" Background="Transparent" BorderThickness="0"
                                  IsChecked="{Binding IsDropDownOpen, RelativeSource={RelativeSource TemplatedParent}, Mode=TwoWay}">
                        <ToggleButton.Template>
                            <ControlTemplate TargetType="ToggleButton"><Border Background="Transparent"/></ControlTemplate>
                        </ToggleButton.Template>
                    </ToggleButton>
                </Grid>
            </Border>
            <Popup x:Name="PART_Popup" Placement="Bottom" AllowsTransparency="True" Focusable="False" PopupAnimation="Fade" IsOpen="{TemplateBinding IsDropDownOpen}">
                <Border Margin="0,5,0,0" MinWidth="{Binding ActualWidth, RelativeSource={RelativeSource TemplatedParent}}" MaxHeight="320"
                        Background="White" BorderBrush="{StaticResource BorderBrush}" BorderThickness="1" CornerRadius="10" Padding="5">
                    <ScrollViewer CanContentScroll="True" VerticalScrollBarVisibility="Auto"><ItemsPresenter/></ScrollViewer>
                </Border>
            </Popup>
        </Grid>
        <ControlTemplate.Triggers>
            <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="ComboBorder" Property="BorderBrush" Value="#FFB8C2CF"/></Trigger>
            <Trigger Property="IsKeyboardFocusWithin" Value="True"><Setter TargetName="ComboBorder" Property="BorderBrush" Value="{StaticResource AccentBrush}"/></Trigger>
            <Trigger Property="IsEnabled" Value="False"><Setter TargetName="ComboBorder" Property="Opacity" Value="0.55"/></Trigger>
        </ControlTemplate.Triggers>
    </ControlTemplate>

    <Style TargetType="ComboBox">
        <Setter Property="FontFamily" Value="{StaticResource BodyFont}"/>
        <Setter Property="FontSize" Value="14"/>
        <Setter Property="MinHeight" Value="44"/>
        <Setter Property="Foreground" Value="{StaticResource TextBrush}"/>
        <Setter Property="Background" Value="{StaticResource SurfaceBrush}"/>
        <Setter Property="BorderBrush" Value="{StaticResource BorderBrush}"/>
        <Setter Property="BorderThickness" Value="1"/>
        <Setter Property="VerticalContentAlignment" Value="Center"/>
        <Setter Property="HorizontalContentAlignment" Value="Left"/>
        <Setter Property="FocusVisualStyle" Value="{x:Null}"/>
        <Setter Property="Template" Value="{StaticResource ModernComboBoxTemplate}"/>
    </Style>

    <ControlTemplate x:Key="ModernComboBoxItemTemplate" TargetType="ComboBoxItem">
        <Border x:Name="ItemBorder" Background="Transparent" CornerRadius="7" Padding="12,9">
            <ContentPresenter VerticalAlignment="Center" HorizontalAlignment="Left" SnapsToDevicePixels="True"/>
        </Border>
        <ControlTemplate.Triggers>
            <Trigger Property="IsHighlighted" Value="True"><Setter TargetName="ItemBorder" Property="Background" Value="{StaticResource HoverBrush}"/></Trigger>
            <Trigger Property="IsSelected" Value="True"><Setter TargetName="ItemBorder" Property="Background" Value="{StaticResource SelectionBrush}"/></Trigger>
        </ControlTemplate.Triggers>
    </ControlTemplate>

    <Style TargetType="ComboBoxItem">
        <Setter Property="FontFamily" Value="{StaticResource BodyFont}"/>
        <Setter Property="FontSize" Value="14"/>
        <Setter Property="Foreground" Value="{StaticResource TextBrush}"/>
        <Setter Property="MinHeight" Value="40"/>
        <Setter Property="HorizontalContentAlignment" Value="Stretch"/>
        <Setter Property="VerticalContentAlignment" Value="Center"/>
        <Setter Property="Template" Value="{StaticResource ModernComboBoxItemTemplate}"/>
    </Style>

    <Style TargetType="DatePickerTextBox" BasedOn="{StaticResource {x:Type TextBox}}">
        <Setter Property="Background" Value="Transparent"/>
        <Setter Property="BorderThickness" Value="0"/>
        <Setter Property="Padding" Value="0"/>
    </Style>

    <ControlTemplate x:Key="ModernDatePickerTemplate" TargetType="DatePicker">
        <Grid SnapsToDevicePixels="True">
            <Border x:Name="DateBorder" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}" CornerRadius="8">
                <Grid>
                    <Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="38"/></Grid.ColumnDefinitions>
                    <DatePickerTextBox x:Name="PART_TextBox" Grid.Column="0" VerticalContentAlignment="Center" Focusable="True"/>
                    <Button x:Name="PART_Button" Grid.Column="1" Margin="2" Padding="0" MinWidth="0" MinHeight="0" BorderThickness="0" Background="Transparent" FontFamily="Segoe MDL2 Assets" FontSize="15" Content="&#xE787;"/>
                </Grid>
            </Border>
            <Popup x:Name="PART_Popup" Placement="Bottom"
                   PlacementTarget="{Binding RelativeSource={RelativeSource TemplatedParent}}"
                   HorizontalOffset="0" VerticalOffset="6"
                   AllowsTransparency="True" Focusable="False" StaysOpen="False"
                   PopupAnimation="Fade"
                   IsOpen="{Binding IsDropDownOpen, RelativeSource={RelativeSource TemplatedParent}}">
                <Grid Width="336" MaxHeight="430" Margin="0,8,0,0" SnapsToDevicePixels="True">
                    <Border Margin="4" Background="White" CornerRadius="16">
                        <Border.Effect>
                            <DropShadowEffect Color="#33000000" BlurRadius="22" ShadowDepth="5" Opacity="0.45"/>
                        </Border.Effect>
                    </Border>
                    <Calendar x:Name="PART_Calendar" Width="336"/>
                </Grid>
            </Popup>
        </Grid>
        <ControlTemplate.Triggers>
            <Trigger Property="IsKeyboardFocusWithin" Value="True"><Setter TargetName="DateBorder" Property="BorderBrush" Value="{StaticResource AccentBrush}"/></Trigger>
            <Trigger Property="IsEnabled" Value="False"><Setter TargetName="DateBorder" Property="Opacity" Value="0.55"/></Trigger>
        </ControlTemplate.Triggers>
    </ControlTemplate>

    <Style TargetType="DatePicker">
        <Setter Property="FontFamily" Value="{StaticResource BodyFont}"/>
        <Setter Property="FontSize" Value="14"/>
        <Setter Property="MinHeight" Value="44"/>
        <Setter Property="Foreground" Value="{StaticResource TextBrush}"/>
        <Setter Property="Background" Value="{StaticResource SurfaceBrush}"/>
        <Setter Property="BorderBrush" Value="{StaticResource BorderBrush}"/>
        <Setter Property="BorderThickness" Value="1"/>
        <Setter Property="VerticalContentAlignment" Value="Center"/>
        <Setter Property="Template" Value="{StaticResource ModernDatePickerTemplate}"/>
    </Style>

    <Style TargetType="Calendar">
        <Setter Property="FontFamily" Value="{StaticResource BodyFont}"/>
        <Setter Property="FontSize" Value="14"/>
        <Setter Property="Background" Value="White"/>
        <Setter Property="BorderBrush" Value="{StaticResource BorderBrush}"/>
        <Setter Property="BorderThickness" Value="1"/>
    </Style>

    <ControlTemplate x:Key="ModernCalendarDayTemplate" TargetType="CalendarDayButton">
        <Border x:Name="DayBorder" Width="34" Height="34" Margin="3" CornerRadius="17" Background="Transparent" BorderBrush="Transparent" BorderThickness="1">
            <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
        </Border>
        <ControlTemplate.Triggers>
            <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="DayBorder" Property="Background" Value="{StaticResource HoverBrush}"/></Trigger>
            <Trigger Property="IsToday" Value="True"><Setter TargetName="DayBorder" Property="BorderBrush" Value="{StaticResource AccentBrush}"/><Setter TargetName="DayBorder" Property="BorderThickness" Value="1"/></Trigger>
            <Trigger Property="IsSelected" Value="True"><Setter TargetName="DayBorder" Property="Background" Value="{StaticResource AccentBrush}"/><Setter Property="Foreground" Value="White"/></Trigger>
            <Trigger Property="IsInactive" Value="True"><Setter Property="Opacity" Value="0.32"/></Trigger>
            <Trigger Property="IsBlackedOut" Value="True"><Setter Property="Opacity" Value="0.28"/><Setter Property="IsHitTestVisible" Value="False"/></Trigger>
            <Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="DayBorder" Property="BorderBrush" Value="{StaticResource AccentBrush}"/></Trigger>
        </ControlTemplate.Triggers>
    </ControlTemplate>

    <Style TargetType="CalendarDayButton">
        <Setter Property="FontFamily" Value="{StaticResource BodyFont}"/>
        <Setter Property="FontSize" Value="14"/>
        <Setter Property="Foreground" Value="{StaticResource TextBrush}"/>
        <Setter Property="HorizontalContentAlignment" Value="Center"/>
        <Setter Property="VerticalContentAlignment" Value="Center"/>
        <Setter Property="Template" Value="{StaticResource ModernCalendarDayTemplate}"/>
    </Style>

    <ControlTemplate x:Key="ModernCalendarButtonTemplate" TargetType="CalendarButton">
        <Border x:Name="YearMonthBorder" Margin="4" Padding="10,12" CornerRadius="10" Background="Transparent">
            <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
        </Border>
        <ControlTemplate.Triggers>
            <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="YearMonthBorder" Property="Background" Value="{StaticResource HoverBrush}"/></Trigger>
            <Trigger Property="HasSelectedDays" Value="True"><Setter TargetName="YearMonthBorder" Property="Background" Value="{StaticResource SelectionBrush}"/><Setter Property="Foreground" Value="{StaticResource AccentBrush}"/></Trigger>
            <Trigger Property="IsInactive" Value="True"><Setter Property="Opacity" Value="0.40"/></Trigger>
        </ControlTemplate.Triggers>
    </ControlTemplate>

    <Style TargetType="CalendarButton">
        <Setter Property="FontFamily" Value="{StaticResource BodyFont}"/>
        <Setter Property="FontSize" Value="13"/>
        <Setter Property="FontWeight" Value="SemiBold"/>
        <Setter Property="Template" Value="{StaticResource ModernCalendarButtonTemplate}"/>
    </Style>

    <Style x:Key="CalendarNavigationButtonStyle" TargetType="Button">
        <Setter Property="Width" Value="36"/>
        <Setter Property="Height" Value="36"/>
        <Setter Property="MinWidth" Value="36"/>
        <Setter Property="MinHeight" Value="36"/>
        <Setter Property="Padding" Value="0"/>
        <Setter Property="FontFamily" Value="Segoe UI"/>
        <Setter Property="FontSize" Value="20"/>
        <Setter Property="FontWeight" Value="Normal"/>
        <Setter Property="Background" Value="Transparent"/>
        <Setter Property="BorderBrush" Value="Transparent"/>
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="Button">
                    <Border x:Name="NavSurface" Background="{TemplateBinding Background}" CornerRadius="8">
                        <ContentPresenter HorizontalAlignment="{TemplateBinding HorizontalContentAlignment}" VerticalAlignment="Center" Margin="{TemplateBinding Padding}"/>
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="NavSurface" Property="Background" Value="{StaticResource HoverBrush}"/></Trigger>
                        <Trigger Property="IsPressed" Value="True"><Setter TargetName="NavSurface" Property="Background" Value="{StaticResource PressedBrush}"/></Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <Style x:Key="CalendarHeaderButtonStyle" TargetType="Button" BasedOn="{StaticResource CalendarNavigationButtonStyle}">
        <Setter Property="Width" Value="Auto"/>
        <Setter Property="MinWidth" Value="140"/>
        <Setter Property="HorizontalContentAlignment" Value="Left"/>
        <Setter Property="FontFamily" Value="{StaticResource BodyFont}"/>
        <Setter Property="FontSize" Value="16"/>
        <Setter Property="FontWeight" Value="SemiBold"/>
        <Setter Property="Padding" Value="12,0"/>
    </Style>

    <Style TargetType="CalendarItem">
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="CalendarItem">
                    <ControlTemplate.Resources>
                        <DataTemplate x:Key="{x:Static CalendarItem.DayTitleTemplateResourceKey}">
                            <TextBlock Text="{Binding}" FontFamily="{StaticResource BodyFont}" FontSize="12" FontWeight="SemiBold" Foreground="{StaticResource MutedBrush}" HorizontalAlignment="Center" VerticalAlignment="Center"/>
                        </DataTemplate>
                    </ControlTemplate.Resources>
                    <Border Background="White" BorderBrush="{StaticResource BorderBrush}" BorderThickness="1" CornerRadius="16" Padding="14" SnapsToDevicePixels="True">
                        <Grid x:Name="PART_Root">
                            <Grid.RowDefinitions><RowDefinition Height="50"/><RowDefinition Height="Auto"/></Grid.RowDefinitions>
                            <Grid Grid.Row="0">
                                <Grid.ColumnDefinitions><ColumnDefinition Width="40"/><ColumnDefinition Width="*"/><ColumnDefinition Width="40"/></Grid.ColumnDefinitions>
                                <Button x:Name="PART_PreviousButton" Grid.Column="0" Content="‹" Style="{StaticResource CalendarNavigationButtonStyle}"/>
                                <Button x:Name="PART_HeaderButton" Grid.Column="1" Style="{StaticResource CalendarHeaderButtonStyle}"/>
                                <Button x:Name="PART_NextButton" Grid.Column="2" Content="›" Style="{StaticResource CalendarNavigationButtonStyle}"/>
                            </Grid>
                            <Grid x:Name="PART_MonthView" Grid.Row="1" Margin="0,4,0,0">
                                <Grid.ColumnDefinitions>
                                    <ColumnDefinition/><ColumnDefinition/><ColumnDefinition/><ColumnDefinition/><ColumnDefinition/><ColumnDefinition/><ColumnDefinition/>
                                </Grid.ColumnDefinitions>
                                <Grid.RowDefinitions>
                                    <RowDefinition Height="34"/><RowDefinition Height="40"/><RowDefinition Height="40"/><RowDefinition Height="40"/><RowDefinition Height="40"/><RowDefinition Height="40"/><RowDefinition Height="40"/>
                                </Grid.RowDefinitions>
                            </Grid>
                            <Grid x:Name="PART_YearView" Grid.Row="1" Margin="0,8,0,0" Visibility="Collapsed">
                                <Grid.ColumnDefinitions><ColumnDefinition/><ColumnDefinition/><ColumnDefinition/><ColumnDefinition/></Grid.ColumnDefinitions>
                                <Grid.RowDefinitions><RowDefinition Height="58"/><RowDefinition Height="58"/><RowDefinition Height="58"/></Grid.RowDefinitions>
                            </Grid>
                        </Grid>
                    </Border>
                    <ControlTemplate.Triggers>
                        <DataTrigger Binding="{Binding DisplayMode, RelativeSource={RelativeSource AncestorType={x:Type Calendar}}}" Value="Year"><Setter TargetName="PART_MonthView" Property="Visibility" Value="Collapsed"/><Setter TargetName="PART_YearView" Property="Visibility" Value="Visible"/></DataTrigger>
                        <DataTrigger Binding="{Binding DisplayMode, RelativeSource={RelativeSource AncestorType={x:Type Calendar}}}" Value="Decade"><Setter TargetName="PART_MonthView" Property="Visibility" Value="Collapsed"/><Setter TargetName="PART_YearView" Property="Visibility" Value="Visible"/></DataTrigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <ControlTemplate x:Key="ModernCheckBoxTemplate" TargetType="CheckBox">
        <Grid SnapsToDevicePixels="True">
            <Grid.ColumnDefinitions><ColumnDefinition Width="22"/><ColumnDefinition Width="*"/></Grid.ColumnDefinitions>
            <Border x:Name="CheckBorder" Width="18" Height="18" CornerRadius="5" BorderBrush="{StaticResource BorderBrush}" BorderThickness="1" Background="White" VerticalAlignment="Center">
                <TextBlock x:Name="CheckGlyph" Text="✓" FontFamily="{StaticResource BodyFont}" FontSize="13" FontWeight="Bold" Foreground="White" HorizontalAlignment="Center" VerticalAlignment="Center" Visibility="Collapsed" Margin="0,-1,0,0"/>
            </Border>
            <ContentPresenter Grid.Column="1" Margin="6,0,0,0" VerticalAlignment="Center" RecognizesAccessKey="True"/>
        </Grid>
        <ControlTemplate.Triggers>
            <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="CheckBorder" Property="BorderBrush" Value="{StaticResource AccentBrush}"/></Trigger>
            <Trigger Property="IsChecked" Value="True"><Setter TargetName="CheckBorder" Property="Background" Value="{StaticResource AccentBrush}"/><Setter TargetName="CheckBorder" Property="BorderBrush" Value="{StaticResource AccentBrush}"/><Setter TargetName="CheckGlyph" Property="Visibility" Value="Visible"/></Trigger>
            <Trigger Property="IsEnabled" Value="False"><Setter Property="Opacity" Value="0.48"/></Trigger>
        </ControlTemplate.Triggers>
    </ControlTemplate>

    <Style TargetType="CheckBox">
        <Setter Property="FontFamily" Value="{StaticResource BodyFont}"/>
        <Setter Property="FontSize" Value="14"/>
        <Setter Property="Foreground" Value="{StaticResource TextBrush}"/>
        <Setter Property="VerticalContentAlignment" Value="Center"/>
        <Setter Property="FocusVisualStyle" Value="{x:Null}"/>
        <Setter Property="Template" Value="{StaticResource ModernCheckBoxTemplate}"/>
    </Style>

    <ControlTemplate x:Key="ModernRadioButtonTemplate" TargetType="RadioButton">
        <Grid SnapsToDevicePixels="True">
            <Grid.ColumnDefinitions><ColumnDefinition Width="22"/><ColumnDefinition Width="*"/></Grid.ColumnDefinitions>
            <Border x:Name="RadioBorder" Width="18" Height="18" CornerRadius="9" BorderBrush="{StaticResource BorderBrush}" BorderThickness="1" Background="White" VerticalAlignment="Center">
                <Ellipse x:Name="RadioDot" Width="8" Height="8" Fill="White" Visibility="Collapsed"/>
            </Border>
            <ContentPresenter Grid.Column="1" Margin="6,0,0,0" VerticalAlignment="Center" RecognizesAccessKey="True"/>
        </Grid>
        <ControlTemplate.Triggers>
            <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="RadioBorder" Property="BorderBrush" Value="{StaticResource AccentBrush}"/></Trigger>
            <Trigger Property="IsChecked" Value="True"><Setter TargetName="RadioBorder" Property="Background" Value="{StaticResource AccentBrush}"/><Setter TargetName="RadioBorder" Property="BorderBrush" Value="{StaticResource AccentBrush}"/><Setter TargetName="RadioDot" Property="Visibility" Value="Visible"/></Trigger>
            <Trigger Property="IsEnabled" Value="False"><Setter Property="Opacity" Value="0.48"/></Trigger>
        </ControlTemplate.Triggers>
    </ControlTemplate>

    <Style TargetType="RadioButton">
        <Setter Property="FontFamily" Value="{StaticResource BodyFont}"/>
        <Setter Property="FontSize" Value="14"/>
        <Setter Property="Foreground" Value="{StaticResource TextBrush}"/>
        <Setter Property="VerticalContentAlignment" Value="Center"/>
        <Setter Property="FocusVisualStyle" Value="{x:Null}"/>
        <Setter Property="Template" Value="{StaticResource ModernRadioButtonTemplate}"/>
    </Style>

    <Style TargetType="DataGrid">
        <Setter Property="FontFamily" Value="{StaticResource BodyFont}"/>
        <Setter Property="FontSize" Value="13"/>
        <Setter Property="Foreground" Value="{StaticResource TextBrush}"/>
        <Setter Property="Background" Value="White"/>
        <Setter Property="BorderBrush" Value="Transparent"/>
        <Setter Property="BorderThickness" Value="0"/>
        <Setter Property="GridLinesVisibility" Value="Horizontal"/>
        <Setter Property="HorizontalGridLinesBrush" Value="{StaticResource SoftBorderBrush}"/>
        <Setter Property="VerticalGridLinesBrush" Value="Transparent"/>
        <Setter Property="HeadersVisibility" Value="Column"/>
        <Setter Property="RowHeight" Value="48"/>
        <Setter Property="ColumnHeaderHeight" Value="44"/>
        <Setter Property="IsReadOnly" Value="True"/>
        <Setter Property="SelectionMode" Value="Single"/>
        <Setter Property="SelectionUnit" Value="FullRow"/>
        <Setter Property="CanUserAddRows" Value="False"/>
        <Setter Property="CanUserDeleteRows" Value="False"/>
        <Setter Property="CanUserResizeRows" Value="False"/>
        <Setter Property="AutoGenerateColumns" Value="True"/>
        <Setter Property="AlternatingRowBackground" Value="#FFFCFDFE"/>
        <Setter Property="RowBackground" Value="White"/>
        <Setter Property="UseLayoutRounding" Value="True"/>
        <Setter Property="SnapsToDevicePixels" Value="True"/>
        <Setter Property="TextOptions.TextFormattingMode" Value="Display"/>
        <Setter Property="TextOptions.TextHintingMode" Value="Fixed"/>
        <Setter Property="TextOptions.TextRenderingMode" Value="ClearType"/>
    </Style>

    <Style TargetType="DataGridColumnHeader">
        <Setter Property="FontFamily" Value="{StaticResource BodyFont}"/>
        <Setter Property="FontSize" Value="13"/>
        <Setter Property="FontWeight" Value="SemiBold"/>
        <Setter Property="Foreground" Value="{StaticResource MutedBrush}"/>
        <Setter Property="Background" Value="#FFF7F9FB"/>
        <Setter Property="BorderBrush" Value="{StaticResource SoftBorderBrush}"/>
        <Setter Property="BorderThickness" Value="0,0,0,1"/>
        <Setter Property="Padding" Value="16,0"/>
        <Setter Property="HorizontalContentAlignment" Value="Left"/>
        <Setter Property="VerticalContentAlignment" Value="Center"/>
        <Setter Property="UseLayoutRounding" Value="True"/>
        <Setter Property="SnapsToDevicePixels" Value="True"/>
    </Style>

    <Style TargetType="DataGridRow">
        <Setter Property="BorderThickness" Value="0"/>
        <Setter Property="Background" Value="Transparent"/>
        <Setter Property="VerticalContentAlignment" Value="Center"/>
        <Style.Triggers>
            <Trigger Property="IsMouseOver" Value="True"><Setter Property="Background" Value="#FFF4F8FD"/></Trigger>
            <Trigger Property="IsSelected" Value="True"><Setter Property="Background" Value="{StaticResource SelectionBrush}"/><Setter Property="Foreground" Value="{StaticResource TextBrush}"/></Trigger>
        </Style.Triggers>
    </Style>

    <Style TargetType="DataGridCell">
        <Setter Property="BorderThickness" Value="0"/>
        <Setter Property="Padding" Value="0"/>
        <Setter Property="HorizontalContentAlignment" Value="Stretch"/>
        <Setter Property="VerticalContentAlignment" Value="Center"/>
        <Setter Property="FocusVisualStyle" Value="{x:Null}"/>
        <Setter Property="UseLayoutRounding" Value="True"/>
        <Setter Property="SnapsToDevicePixels" Value="True"/>
        <Style.Triggers>
            <Trigger Property="IsSelected" Value="True"><Setter Property="Background" Value="Transparent"/><Setter Property="Foreground" Value="{StaticResource TextBrush}"/></Trigger>
        </Style.Triggers>
    </Style>

    <Style TargetType="ToolTip">
        <Setter Property="FontFamily" Value="{StaticResource BodyFont}"/>
        <Setter Property="FontSize" Value="12"/>
        <Setter Property="Foreground" Value="White"/>
        <Setter Property="Background" Value="#F21A1F27"/>
        <Setter Property="Padding" Value="10,7"/>
        <Setter Property="HasDropShadow" Value="True"/>
    </Style>

    <Style x:Key="InvisibleRepeatButtonStyle" TargetType="RepeatButton">
        <Setter Property="Focusable" Value="False"/>
        <Setter Property="Template">
            <Setter.Value><ControlTemplate TargetType="RepeatButton"><Border Background="Transparent"/></ControlTemplate></Setter.Value>
        </Setter>
    </Style>

    <Style x:Key="ModernScrollThumbStyle" TargetType="Thumb">
        <Setter Property="MinWidth" Value="24"/>
        <Setter Property="MinHeight" Value="24"/>
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="Thumb">
                    <Border x:Name="ThumbSurface" Margin="3" Background="#737E8B99" CornerRadius="4"/>
                    <ControlTemplate.Triggers>
                        <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="ThumbSurface" Property="Background" Value="#9A6B7787"/></Trigger>
                        <Trigger Property="IsDragging" Value="True"><Setter TargetName="ThumbSurface" Property="Background" Value="#B35D6978"/></Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <Style TargetType="ScrollBar">
        <Setter Property="Background" Value="Transparent"/>
        <Setter Property="Width" Value="12"/>
        <Setter Property="Height" Value="Auto"/>
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="ScrollBar">
                    <Grid Background="{TemplateBinding Background}">
                        <Track x:Name="PART_Track" Orientation="{TemplateBinding Orientation}" IsDirectionReversed="True">
                            <Track.DecreaseRepeatButton><RepeatButton x:Name="DecreaseButton" Command="ScrollBar.PageUpCommand" Style="{StaticResource InvisibleRepeatButtonStyle}"/></Track.DecreaseRepeatButton>
                            <Track.Thumb><Thumb Style="{StaticResource ModernScrollThumbStyle}"/></Track.Thumb>
                            <Track.IncreaseRepeatButton><RepeatButton x:Name="IncreaseButton" Command="ScrollBar.PageDownCommand" Style="{StaticResource InvisibleRepeatButtonStyle}"/></Track.IncreaseRepeatButton>
                        </Track>
                    </Grid>
                    <ControlTemplate.Triggers>
                        <Trigger Property="Orientation" Value="Horizontal">
                            <Setter TargetName="PART_Track" Property="IsDirectionReversed" Value="False"/>
                            <Setter TargetName="DecreaseButton" Property="Command" Value="ScrollBar.PageLeftCommand"/>
                            <Setter TargetName="IncreaseButton" Property="Command" Value="ScrollBar.PageRightCommand"/>
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
        <Style.Triggers>
            <Trigger Property="Orientation" Value="Horizontal">
                <Setter Property="Width" Value="Auto"/>
                <Setter Property="Height" Value="12"/>
            </Trigger>
        </Style.Triggers>
    </Style>

</ResourceDictionary>
""";
}
