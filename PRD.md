 (cd "$(git rev-parse --show-toplevel)" && git apply --3way <<'EOF' 
diff --git a/PRD.md b/PRD.md
new file mode 100644
index 0000000000000000000000000000000000000000..4bfec17f43438ed5164260195a8f8169d375f970
--- /dev/null
+++ b/PRD.md
@@ -0,0 +1,193 @@
+# PRD — Ứng dụng Thuyết Minh Ẩm Thực (FoodGuide)
+
+## 1) Tổng quan sản phẩm
+**Tên sản phẩm:** Thuyết Minh Ẩm Thực (FoodGuide).  
+**Mục tiêu:** Xây dựng hệ thống hướng dẫn tham quan ẩm thực theo vị trí (geofence), tự động phát thuyết minh đa ngôn ngữ bằng TTS khi người dùng đi gần/đi vào từng điểm POI (Point of Interest).
+
+Hệ thống hiện tại gồm:
+- **Mobile app (.NET MAUI):** hiển thị bản đồ, lấy GPS, theo dõi geofence, phát audio thuyết minh.
+- **Web API (.NET):** quản lý POI, bản dịch, đăng nhập quản trị, và API tạo audio TTS.
+
+---
+
+## 2) Bài toán & cơ hội
+### Bài toán người dùng
+- Khách tham quan khó nắm được thông tin món ăn/điểm dừng một cách liền mạch khi đang di chuyển.
+- Hướng dẫn viên thủ công không đảm bảo tính nhất quán và mở rộng cho đa ngôn ngữ.
+- Nội dung thuyết minh cần được cập nhật nhanh theo từng điểm POI.
+
+### Cơ hội
+- Tự động hóa thuyết minh theo vị trí giúp trải nghiệm “hands-free”.
+- Đa ngôn ngữ tăng khả năng phục vụ khách quốc tế.
+- Tạo nền tảng mở rộng cho các tuyến tham quan khác (ẩm thực, di tích, bảo tàng...).
+
+---
+
+## 3) Mục tiêu sản phẩm
+### Mục tiêu ngắn hạn (MVP)
+1. Người dùng xem POI trên bản đồ và theo dõi vị trí theo thời gian thực.
+2. Hệ thống nhận diện trạng thái **gần**/**vào vùng geofence** của POI.
+3. Phát audio thuyết minh tự động cho POI tương ứng.
+4. Quản trị viên đăng nhập và quản lý POI + nội dung bản dịch.
+
+### Mục tiêu trung hạn
+1. Cải thiện độ ổn định geofence trong môi trường GPS nhiễu.
+2. Tối ưu pipeline TTS (cache audio, giảm độ trễ).
+3. Bổ sung theo dõi sự kiện để đo hiệu quả nội dung.
+
+### Mục tiêu dài hạn
+1. CMS nội dung hoàn chỉnh cho nhiều tuyến/địa điểm.
+2. Gợi ý hành trình cá nhân hóa theo sở thích người dùng.
+3. Hỗ trợ offline mode cho vùng mạng yếu.
+
+---
+
+## 4) Đối tượng người dùng
+1. **Khách tham quan**
+   - Muốn trải nghiệm thuận tiện, không cần thao tác nhiều.
+   - Cần thông tin ngắn gọn, đúng lúc, đúng địa điểm.
+2. **Quản trị nội dung (Admin)**
+   - Quản lý POI, nội dung thuyết minh, bản dịch.
+   - Theo dõi tính đầy đủ/chất lượng dữ liệu.
+3. **Vận hành hệ thống**
+   - Đảm bảo API, TTS, dữ liệu hoạt động ổn định.
+
+---
+
+## 5) Phạm vi sản phẩm
+### In scope (MVP)
+- Mobile app:
+  - Tải POI từ API.
+  - Hiển thị POI trên bản đồ.
+  - Start/Stop tracking vị trí.
+  - Xử lý geofence và phát audio.
+  - Chuyển ngôn ngữ cơ bản (vi/en/zh).
+- Backend API:
+  - CRUD POI.
+  - CRUD bản dịch theo POI.
+  - API đăng nhập/đăng ký admin.
+  - API tạo audio TTS.
+
+### Out of scope (MVP)
+- Thanh toán, bán vé.
+- Chức năng mạng xã hội.
+- AI recommendation nâng cao.
+- Dashboard BI chuyên sâu.
+
+---
+
+## 6) Yêu cầu chức năng
+### 6.1 Mobile App
+1. **Bản đồ & POI**
+   - Hiển thị bản đồ nền.
+   - Vẽ marker POI và vòng geofence.
+   - Lọc POI có tọa độ hợp lệ.
+2. **Theo dõi vị trí**
+   - Xin quyền vị trí.
+   - Cập nhật vị trí liên tục khi bật tracking.
+   - Hiển thị POI gần nhất theo khoảng cách.
+3. **Luồng geofence**
+   - Nhận biết trạng thái `outside -> near -> inside`.
+   - Có debounce/cooldown tránh phát lặp audio.
+4. **Audio/TTS**
+   - Gọi API TTS với `poiId`, `text`, `language`.
+   - Quản lý hàng đợi audio, tránh chồng tiếng.
+5. **Cài đặt**
+   - Base URL API cấu hình được.
+   - Chọn ngôn ngữ phát thuyết minh.
+
+### 6.2 Web API
+1. **POI API**
+   - Lấy danh sách POI active.
+   - Lấy chi tiết POI theo ID.
+   - Tạo/cập nhật/xóa POI.
+2. **Translation API**
+   - Lấy bản dịch theo POI.
+   - Lưu/cập nhật bản dịch theo ngôn ngữ.
+   - Xóa bản dịch.
+3. **Auth API**
+   - Đăng nhập admin bằng username/password.
+   - Đăng ký admin mới (vai trò, poiId phụ trách).
+4. **TTS API**
+   - Endpoint generate audio URL.
+   - Endpoint kiểm tra tình trạng dịch vụ.
+
+---
+
+## 7) Yêu cầu phi chức năng
+1. **Hiệu năng**
+   - Thời gian phản hồi API phổ biến < 500ms (không tính TTS generate dài).
+   - Theo dõi vị trí mượt, không giật UI.
+2. **Độ tin cậy**
+   - Geofence không spam trigger (cooldown tối thiểu 5 giây).
+   - Tự phục hồi nếu lỗi GPS tạm thời.
+3. **Bảo mật**
+   - Mật khẩu admin lưu dạng hash.
+   - Không hardcode secret key trong app.
+   - Bổ sung JWT/role-based authorization trong giai đoạn tiếp theo.
+4. **Khả năng mở rộng**
+   - Thiết kế dữ liệu hỗ trợ thêm ngôn ngữ và tuyến mới.
+
+---
+
+## 8) User stories ưu tiên cao
+1. **Khách tham quan**: “Khi tôi đi vào gần một quầy món ăn, tôi muốn tự động nghe thuyết minh bằng ngôn ngữ tôi chọn.”
+2. **Khách tham quan**: “Tôi muốn biết mình đang gần điểm nào trên bản đồ.”
+3. **Admin**: “Tôi muốn thêm/sửa/xóa POI để cập nhật tuyến tham quan.”
+4. **Admin**: “Tôi muốn cập nhật nội dung dịch cho từng ngôn ngữ để phục vụ khách quốc tế.”
+
+---
+
+## 9) Chỉ số thành công (KPIs)
+1. **Activation rate**: % người dùng bật tracking sau khi mở app.
+2. **POI trigger success rate**: % lượt vào geofence có phát audio thành công.
+3. **Average session time**: thời gian sử dụng trong một chuyến tham quan.
+4. **Completion rate**: % người dùng nghe >= N điểm POI trong tuyến.
+5. **Error rate**: tỷ lệ lỗi API/TTS/GPS.
+
+---
+
+## 10) Rủi ro & phương án giảm thiểu
+1. **GPS sai số cao**
+   - Giải pháp: debounce + near radius + cooldown, làm mượt vị trí.
+2. **TTS chậm hoặc lỗi dịch vụ ngoài**
+   - Giải pháp: fallback text, retry, cache audio theo `(poiId, language, version)`.
+3. **Nội dung không đồng nhất giữa ngôn ngữ**
+   - Giải pháp: quy trình duyệt nội dung, cảnh báo thiếu bản dịch.
+4. **Bảo mật auth còn cơ bản**
+   - Giải pháp: bổ sung JWT, refresh token, phân quyền chi tiết.
+
+---
+
+## 11) Lộ trình đề xuất
+### Phase 1 — MVP ổn định (2–4 tuần)
+- Chuẩn hóa CRUD POI/Translation.
+- Hoàn thiện luồng geofence + audio queue.
+- Bổ sung logging lỗi cơ bản.
+
+### Phase 2 — Production readiness (4–8 tuần)
+- JWT auth + authorization.
+- Audit log thao tác admin.
+- Tối ưu cache audio và retry policy.
+
+### Phase 3 — Scale & analytics (8+ tuần)
+- Dashboard thống kê hành vi nghe.
+- A/B test nội dung thuyết minh.
+- Offline package cho tuyến cố định.
+
+---
+
+## 12) Tiêu chí nghiệm thu MVP
+1. Ứng dụng tải được danh sách POI và hiển thị trên bản đồ.
+2. Khi người dùng di chuyển vào vùng geofence, hệ thống phát audio tương ứng ít nhất 95% trong điều kiện mạng ổn định.
+3. Admin đăng nhập thành công và quản trị được POI + bản dịch.
+4. API tài liệu hóa bằng Swagger, có thể kiểm thử độc lập từng endpoint.
+
+---
+
+## 13) Phụ lục: đề xuất backlog kỹ thuật ngắn
+- [ ] Thêm chuẩn hóa validation DTO phía API.
+- [ ] Áp dụng JWT + policy-based authorization.
+- [ ] Thêm endpoint health check tổng hợp.
+- [ ] Thêm telemetry (App Insights/OpenTelemetry).
+- [ ] Tạo script seed dữ liệu mẫu POI đa ngôn ngữ.
 
EOF
)
