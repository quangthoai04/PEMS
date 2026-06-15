using System;
using System.IO;
using System.Text;

namespace InfraScaffolder
{
    class Program
    {
        static void Main()
        {
            Console.WriteLine("Generating Infra interfaces...");
            
            string rootDir = Directory.GetCurrentDirectory();
            string appInterfacesDir = Path.Combine(rootDir, "backend", "PEMS.Application", "Common", "Interfaces");
            string infraDir = Path.Combine(rootDir, "backend", "PEMS.Infrastructure");
            
            Directory.CreateDirectory(appInterfacesDir);
            
            var interfaces = new[] { 
                "IApplicationDbContext", "IDelegationRepository", "IPartnerRepository", 
                "IDocumentRepository", "IEmailService", "IFileStorageService", 
                "IOcrService", "IFaceRecognitionService", "INotificationService", 
                "IAuditLogService", "IPermissionChecker", "IOwnershipChecker", 
                "IIdempotencyService", "IRateLimitService"
            };

            foreach (var iface in interfaces)
            {
                var filePath = Path.Combine(appInterfacesDir, $"{iface}.cs");
                if (!File.Exists(filePath))
                {
                    File.WriteAllText(filePath, $"namespace PEMS.Application.Common.Interfaces;\n\npublic interface {iface}\n{{\n}}\n");
                }
            }

            // Generate implementations in Infrastructure
            string[] infraFolders = { "Persistence/Repositories", "Email", "FileStorage", "ExternalServices", "Identity", "Logging", "RateLimiting", "Idempotency" };
            foreach (var f in infraFolders) Directory.CreateDirectory(Path.Combine(infraDir, f));

            CreateImpl(infraDir, "Persistence/Repositories", "DelegationRepository", "IDelegationRepository");
            CreateImpl(infraDir, "Persistence/Repositories", "PartnerRepository", "IPartnerRepository");
            CreateImpl(infraDir, "Persistence/Repositories", "DocumentRepository", "IDocumentRepository");
            CreateImpl(infraDir, "Email", "EmailService", "IEmailService");
            CreateImpl(infraDir, "FileStorage", "FileStorageService", "IFileStorageService");
            CreateImpl(infraDir, "ExternalServices", "OcrService", "IOcrService");
            CreateImpl(infraDir, "ExternalServices", "FaceRecognitionService", "IFaceRecognitionService");
            CreateImpl(infraDir, "Identity", "NotificationService", "INotificationService");
            CreateImpl(infraDir, "Logging", "AuditLogService", "IAuditLogService");
            CreateImpl(infraDir, "Identity", "PermissionChecker", "IPermissionChecker");
            CreateImpl(infraDir, "Identity", "OwnershipChecker", "IOwnershipChecker");
            CreateImpl(infraDir, "Idempotency", "IdempotencyService", "IIdempotencyService");
            CreateImpl(infraDir, "RateLimiting", "RateLimitService", "IRateLimitService");
        }

        static void CreateImpl(string root, string folder, string className, string iface)
        {
            var p = Path.Combine(root, folder, $"{className}.cs");
            if (!File.Exists(p))
            {
                var ns = "PEMS.Infrastructure." + folder.Replace("/", ".");
                File.WriteAllText(p, $"using PEMS.Application.Common.Interfaces;\n\nnamespace {ns};\n\npublic class {className} : {iface}\n{{\n}}\n");
            }
        }
    }
}
