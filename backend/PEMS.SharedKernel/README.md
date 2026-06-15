# PEMS Shared Kernel

## Mục đích folder
Thư mục này chứa các thành phần cốt lõi (Core components) dùng chung cho toàn bộ các module trong hệ thống PEMS theo kiến trúc Domain-Driven Design (DDD) và Clean Architecture. Các thành phần ở đây độc lập và không phụ thuộc vào bất kỳ module cụ thể nào.

## Thuộc module nào
Nằm ở trung tâm của Backend (Backend / Shared Kernel), được sử dụng bởi PEMS.Domain, PEMS.Application, và các thành phần khác khi cần chia sẻ logic chung.

## Sẽ chứa loại file gì
- Base classes (ví dụ: `Entity`, `AggregateRoot`, `DomainEvent`).
- Các interfaces dùng chung (ví dụ: `IDomainEventDispatcher`).
- Exceptions dùng chung.
- Value Objects chung không thuộc cụ thể một Bounded Context nào.

## Trạng thái hiện tại
**Placeholder / Pending Implementation**

Hiện tại folder này đang được giữ chỗ. Các class và interface dùng chung sẽ được di chuyển hoặc tạo mới tại đây trong quá trình refactor logic chi tiết tiếp theo.
