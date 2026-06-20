# Hướng dẫn Setup UI Authentication trong Unity

## Bước 1: Tạo Canvas và Panels

### 1.1 Tạo Canvas mới
1. Vào **Hierarchy** → Right click → **UI** → **Canvas**
2. Đặt tên là **"AuthCanvas"**
3. Trong Inspector, set **Render Mode** = **Screen Space - Overlay**
4. Set **Canvas Scaler** → **UI Scale Mode** = **Scale With Screen Size**
   - Reference Resolution: **1920 x 1080**

### 1.2 Tạo các Panels
Tạo 4 Panel con trong AuthCanvas:

```
AuthCanvas
├── LoginPanel
├── RegisterPanel
├── LoadingPanel
└── MainMenuPanel
```

Cách tạo Panel:
1. Right click Canvas → **UI** → **Panel**
2. Đổi màu nền (Image Component) → màu tối (ví dụ: #1A1A1A)
3. Set **Opacity** (Alpha) khoảng **220**

## Bước 2: Setup Login Panel

### 2.1 Cấu trúc LoginPanel
```
LoginPanel
├── Title (TextMeshPro)
├── EmailInput (TMP_InputField)
├── PasswordInput (TMP_InputField)
├── RememberMe (Toggle)
├── LoginButton (Button)
├── ErrorText (TextMeshPro) - Ẩn mặc định
└── RegisterButton (Button - "Chưa có tài khoản?")
```

### 2.2 Tạo UI Elements chi tiết

**1. Title**
- GameObject → UI → Text - TextMeshPro
- Text: "ĐĂNG NHẬP" hoặc "AILIFE"
- Font Size: **48**
- Style: **Bold**
- Color: **White**
- Alignment: **Center**

**2. Email Input**
- GameObject → UI → Input Field - TextMeshPro
- Width: **400**, Height: **60**
- Placeholder: "Nhập email của bạn"
- Content Type: **Email Address**

**3. Password Input**
- Tương tự Email Input
- Placeholder: "Nhập mật khẩu"
- Content Type: **Password** (sẽ ẩn ký tự)

**4. Login Button**
- GameObject → UI → Button - TextMeshPro
- Width: **400**, Height: **60**
- Text: "ĐĂNG NHẬP"
- Font Size: **24**
- Style: **Bold**

**5. Error Text**
- TextMeshPro - màu **Red** (#FF4444)
- Font Size: **18**
- Alignment: **Center**
- Mặc định **Disable** (tắt) GameObject

## Bước 3: Setup Register Panel

### 3.1 Cấu trúc RegisterPanel
```
RegisterPanel
├── Title ("ĐĂNG KÝ")
├── UsernameInput
├── EmailInput
├── PasswordInput
├── ConfirmPasswordInput
├── RegisterButton
├── ErrorText (Red)
├── SuccessText (Green) - "Đăng ký thành công!"
└── BackToLoginButton
```

Tương tự Login Panel, chỉ thêm:
- **Username Input** (Content Type: Standard)
- **Confirm Password Input** (Content Type: Password)
- **Success Text** (màu **Green** #44FF44) - mặc định ẩn

## Bước 4: Setup Loading Panel

### 4.1 Cấu trúc
```
LoadingPanel
├── Background (Image - màu đen opacity 0.8)
├── LoadingText ("Đang đăng nhập...")
└── ProgressBar (Slider)
```

**LoadingText**: 
- TextMeshPro
- Font Size: **24**
- Color: **White**
- Text: "Đang tải..." hoặc "Đang đăng nhập..."

## Bước 5: Gán References vào Script

### 5.1 Tạo Empty GameObject
1. Trong Hierarchy → Right click → **Create Empty**
2. Đặt tên: **"LoginRegisterUI"**
3. Kéo script **LoginRegisterUI.cs** vào component

### 5.2 Gán References trong Inspector

Kéo các UI elements vào các field tương ứng:

```
Login Register UI (Script)
├── Login Panel: [Kéo LoginPanel từ Hierarchy]
├── Register Panel: [Kéo RegisterPanel]
├── Loading Panel: [Kéo LoadingPanel]
├── Main Menu Panel: [Kéo MainMenuPanel]
│
├── Login Email Input: [Kéo EmailInput]
├── Login Password Input: [Kéo PasswordInput]
├── Remember Me Toggle: [Kéo RememberMe]
├── Login Button: [Kéo LoginButton]
├── Show Register Button: [Kéo RegisterButton trên LoginPanel]
├── Login Error Text: [Kéo ErrorText]
│
├── Register Username Input: [Kéo UsernameInput]
├── Register Email Input: [Kéo EmailInput trên RegisterPanel]
├── Register Password Input: [Kéo PasswordInput trên RegisterPanel]
├── Register Confirm Password Input: [Kéo ConfirmPasswordInput]
├── Register Button: [Kéo RegisterButton]
├── Show Login Button: [Kéo BackToLoginButton]
├── Register Error Text: [Kéo ErrorText trên RegisterPanel]
└── Register Success Text: [Kéo SuccessText]
```

## Bước 6: Test trong Unity

1. Nhấn **Play** trong Unity
2. Nếu chưa đăng nhập, sẽ thấy **Login Panel**
3. Thử đăng nhập với email/password test
4. Hoặc click "Chưa có tài khoản?" để sang **Register Panel**
5. Sau khi đăng nhập thành công, sẽ chuyển sang **Main Menu Panel**

---

**Lưu ý quan trọng:**
- Nhớ add **TMPro** namespace đầu file: `using TMPro;`
- Các Input Field dùng **TextMeshPro** (không dùng UI.InputField cũ)
- Cần install package **TextMeshPro** từ Unity Package Manager
