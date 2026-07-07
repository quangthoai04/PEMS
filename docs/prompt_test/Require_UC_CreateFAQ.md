Tôi đã khôi phục lại \`pems\_db\` rồi. Không cần xử lý binary log/point-in-time recovery nữa.

Từ bây giờ, tuyệt đối không động vào \`pems\_db\`.

Tiếp tục thực hiện task theo file:

\`\`\`text  
PROMPT\_AI\_CREATE\_FAQ\_UNIT\_INTEGRATION\_SAFE\_NO\_DOCKER.md  
\`\`\`

Nhưng làm theo thứ tự an toàn sau:

\#\# 1\. Được phép làm ngay

Bạn được phép tạo/sửa các file sau:

\`\`\`text  
tests/PEMS.UnitTests/...  
tests/PEMS.IntegrationTests/...  
tests/PEMS.UnitTests/TestHelpers/...  
tests/PEMS.IntegrationTests/TestInfrastructure/...  
docs/testing/...  
backend/PEMS.Api/appsettings.Testing.example.json  
\`\`\`

Bạn được phép viết Unit Test cho Create FAQ.

Bạn được phép scaffold Integration Test code, TestAuthHandler, PemsWebApplicationFactory, DatabaseResetHelper nếu cần.

Bạn được phép tạo file hướng dẫn setup database test.

Bạn được phép tạo bản copy tạm của SQL fresh-create dành cho \`pems\_test\`, nhưng chưa được import.

\---

\#\# 2\. Chưa được phép chạy DB

Chưa được chạy các lệnh sau nếu chưa báo command cụ thể và được tôi xác nhận:

\`\`\`text  
DROP DATABASE  
CREATE DATABASE  
USE  
mysql \< script.sql  
dotnet test PEMS.IntegrationTests  
\`\`\`

Chưa được import SQL.

Chưa được reset DB.

Chưa được chạy Integration Test thật nếu nó cần database.

\---

\#\# 3\. Quy tắc với SQL fresh-create

Không được sửa file SQL gốc.

Nếu cần dùng SQL fresh-create để tạo \`pems\_test\`, hãy tạo bản copy tạm:

\`\`\`text  
docs/testing/tmp/fresh\_create\_for\_pems\_test.sql  
\`\`\`

Trong bản copy tạm, thay:

\`\`\`text  
pems\_db \-\> pems\_test  
\`\`\`

Sau đó chỉ được scan/kiểm tra file, chưa được chạy import.

Báo cáo lại kết quả scan:

\`\`\`text  
\- Còn pems\_db không?  
\- Còn USE pems\_db không?  
\- Còn DROP DATABASE pems\_db không?  
\- Còn CREATE DATABASE pems\_db không?  
\- Các lệnh DROP/CREATE/USE hiện trỏ tới database nào?  
\`\`\`

Nếu còn bất kỳ dấu vết \`pems\_db\`, phải dừng lại.

\---

\#\# 4\. Quy tắc với MySQL user

Không dùng root nếu chưa cần.

Nếu bắt buộc phải dùng root local để tạo \`pems\_test\`, hãy báo trước command sẽ chạy và chờ tôi xác nhận.

Ưu tiên đề xuất tạo user test riêng chỉ có quyền trên \`pems\_test\`, ví dụ:

\`\`\`text  
pems\_test\_user  
\`\`\`

Không được đọc/copy/in secret thật từ:

\`\`\`text  
appsettings.Development.json  
.env  
Google credential  
SMTP password  
JWT secret  
OAuth secret  
\`\`\`

Nếu cần config, chỉ tạo file example với placeholder.

\---

\#\# 5\. Được phép chạy Unit Test

Bạn được phép chạy Unit Test nếu không cần DB:

\`\`\`bash  
dotnet test tests/PEMS.UnitTests/PEMS.UnitTests.csproj  
\`\`\`

Nếu lệnh khác với cấu trúc project thực tế, hãy báo lệnh dự kiến trước.

\---

\#\# 6\. Integration Test

Với Integration Test:

\- Được viết code.  
\- Được build/compile nếu không cần kết nối DB thật.  
\- Chưa được chạy test thật nếu test đó sẽ tạo/reset/import database.

Trước khi chạy Integration Test thật, phải báo cáo:

\`\`\`text  
1\. Database sẽ dùng là gì?  
2\. Connection string test lấy từ đâu?  
3\. Có dùng appsettings.Development.json không?  
4\. SQL nào sẽ import?  
5\. File SQL đã scan an toàn chưa?  
6\. Command chính xác sẽ chạy là gì?  
\`\`\`

Sau đó dừng lại chờ tôi xác nhận.

\---

\#\# 7\. Không sửa production code

Không được tự ý sửa:

\`\`\`text  
Controller thật  
Handler thật  
Validator thật  
Entity thật  
DbContext thật  
SQL schema thật  
Frontend nghiệp vụ thật  
appsettings.Development.json  
\`\`\`

Nếu test fail vì nghi ngờ production code lỗi, chỉ báo cáo expected/actual và đề xuất hướng sửa. Không tự sửa production code.

\---

\#\# 8\. Việc cần làm ngay bây giờ

Bây giờ hãy tiếp tục theo hướng:

\`\`\`text  
\#\# 8\. Việc cần làm ngay bây giờ

Bây giờ chỉ được làm các việc chuẩn bị, chưa được chạy Integration Test thật nếu cần database.

Hãy thực hiện theo thứ tự:

\`\`\`text  
1\. Tạo/sửa Unit Test code.  
2\. Tạo/sửa Integration Test code nhưng chưa chạy nếu test cần DB.  
3\. Tạo/sửa test infrastructure cần thiết.  
4\. Tạo appsettings.Testing.example.json nếu cần, chỉ dùng placeholder.  
5\. Tạo docs/testing hướng dẫn setup pems\_test nếu cần.  
6\. Tạo bản copy SQL tạm cho pems\_test và scan an toàn.  
7\. Không import SQL.  
8\. Không reset DB.  
9\. Không chạy Integration Test thật nếu test cần DB.  
10\. Có thể chạy Unit Test nếu không cần DB.  
11\. Báo cáo lại toàn bộ file đã tạo/sửa, kết quả scan SQL, và dừng lại chờ tôi duyệt bước DB.

\`\`\`

Nhắc lại: \`pems\_db\` đã khôi phục, không xử lý recovery nữa và không được động vào \`pems\_db\`.  
