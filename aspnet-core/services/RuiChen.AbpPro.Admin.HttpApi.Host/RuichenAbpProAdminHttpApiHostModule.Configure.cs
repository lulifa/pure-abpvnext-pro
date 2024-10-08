﻿using Medallion.Threading.Redis;
using Medallion.Threading;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.OpenApi.Models;
using OpenIddict.Server.AspNetCore;
using StackExchange.Redis;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Auditing;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.GlobalFeatures;
using Volo.Abp.Json;
using Volo.Abp.Json.SystemTextJson;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Volo.Abp.Threading;
using Volo.Abp.UI.Navigation.Urls;
using Volo.Abp.VirtualFileSystem;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.PermissionManagement;
using RuiChen.AbpPro.OpenIddict;
using RuiChen.AbpPro.Saas;
using Volo.Abp.FeatureManagement;
using Volo.Abp.Features;
using Volo.Abp.SettingManagement;
using RuiChen.AbpPro.Localization;
using Volo.Abp.Localization;
using Volo.Abp;
using RuiChen.AbpPro.Wrapper;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Volo.Abp.Caching;
using RuiChen.AbpPro.ExceptionHandling;
using VoloAbpExceptionHandlingOptions = Volo.Abp.AspNetCore.ExceptionHandling.AbpExceptionHandlingOptions;


namespace RuiChen.AbpPro.Admin.HttpApi.Host
{
    public partial class RuichenAbpProAdminHttpApiHostModule
    {

        public static string ApplicationName { get; set; } = "AdminService";

        protected const string DefaultCorsPolicyName = "Default";

        private static readonly OneTimeRunner OneTimeRunner = new OneTimeRunner();


        private void PreConfigureFeature()
        {
            OneTimeRunner.Run(() =>
            {
                GlobalFeatureManager.Instance.Modules.Editions().EnableAll();
            });
        }


        /// <summary>
        /// 确保在身份验证系统的配置阶段添加默认的令牌提供程序
        /// 密码重置令牌：用于在用户请求重置密码时生成和验证的令牌。
        /// 电子邮件确认令牌：用于在用户注册时验证电子邮件地址的令牌。
        /// 更改邮箱令牌：用于在用户更改电子邮件地址时验证的令牌。
        /// 更改密码令牌：用于在用户请求更改密码时生成和验证的令牌。
        /// </summary>
        private void PreConfigureIdentity()
        {
            PreConfigure<IdentityBuilder>(builder =>
            {
                builder.AddDefaultTokenProviders();
            });
        }

        /// <summary>
        /// 通过 OpenIddict 配置令牌验证的相关选项，以支持 OpenID Connect 和 OAuth 2.0 协议
        /// 用于指定授权服务器允许哪些受众（audiences）。受众是用于识别授权服务器颁发的令牌的一个重要参数。
        /// 这里的 "ruichen-abppro-application" 是一个示例受众标识，表示你的应用程序或客户端
        /// 配置 OpenIddict 以使用本地的授权服务器。这个选项指示 OpenIddict 将验证令牌的请求发送到同一应用程序中的本地授权服务器
        /// 配置 OpenIddict 以使用 ASP.NET Core 的中间件来处理身份验证请求。
        /// 这意味着 OpenIddict 将利用 ASP.NET Core 的身份验证系统来处理和验证令牌
        /// </summary>
        /// <param name="configuration"></param>
        private void PreConfigureAuthServer(IConfiguration configuration)
        {
            PreConfigure<OpenIddictBuilder>(builder =>
            {
                builder.AddValidation(options =>
                {
                    options.AddAudiences(configuration["AuthServer:Scope"]);
                    options.UseLocalServer();
                    options.UseAspNetCore();
                });
            });
        }

        private void ConfigureWrapper()
        {
            Configure<AbpWrapperOptions>(options =>
            {
                options.IsEnabled = true;
                options.IgnoreNamespaces.Add("Elsa");
                options.IgnoreNamespaces.Add("RuiChen.AbpPro.OssManagement");
                options.IgnoreNamespaces.Add("RuiChen.AbpPro.WeChat");
            });
        }

        /// <summary>
        /// 配置 ABP 框架中的审计功能
        /// </summary>
        private void ConfigureAuditing(IConfiguration configuration)
        {
            Configure<AbpAuditingOptions>(options =>
            {
                // 启用对 GET 请求的审计
                options.IsEnabledForGetRequests = true;

                // 设置应用程序名称
                options.ApplicationName = ApplicationName;

                var allEntitiesSelectorIsEnabled = configuration["Auditing:AllEntitiesSelector"];
                if (allEntitiesSelectorIsEnabled.IsNullOrWhiteSpace() ||
                    (bool.TryParse(allEntitiesSelectorIsEnabled, out var enabled) && enabled))
                {
                    // 启用对所有实体的历史记录
                    options.EntityHistorySelectors.AddAllEntities();
                }
            });
        }

        /// <summary>
        /// 设置使用 MySQL 数据库作为数据存储
        /// </summary>
        private void ConfigureDbContext()
        {
            Configure<AbpDbContextOptions>(options =>
            {
                // 设置MySql作为数据源
                options.UseMySQL();
            });
        }

        private void ConfigureLocalization()
        {
            Configure<AbpLocalizationOptions>(options =>
            {
                options.Languages.AddRange(new[]
                {
                    new LanguageInfo("en","en","English"),
                    new LanguageInfo("zh-Hans","zh-Hans","简体中文")
                });

                var languagesMap = new Dictionary<string, NameValue[]>
                {
                    {"pure-admin-ui",new[]{new NameValue("zh-Hans","zh"),new NameValue("en", "en")} },
                    {"vben-admin-ui",new[]{new NameValue("zh_CN","zh_Hans")} }
                };

                foreach (var (app, mappings) in languagesMap)
                {
                    options.AddLanguagesMapOrUpdate(app, mappings);
                }
            });

            Configure<AbpProLocalizationCultureMapOptions>(options =>
            {
                var zhHansCultureMapInfo = new CultureMapInfo
                {
                    TargetCulture = "zh-Hans",
                    SourceCultures = new[] { "zh", "zh_CN", "zh-CN" }
                };

                // 添加文化映射信息
                options.CulturesMaps.Add(zhHansCultureMapInfo);
                options.UiCulturesMaps.Add(zhHansCultureMapInfo);
            });
        }


        /// <summary>
        /// 配置 Kestrel 服务器的选项
        /// </summary>
        private void ConfigureKestrelServer()
        {
            Configure<KestrelServerOptions>(options =>
            {
                // Kestrel 对请求体大小有一个限制 允许接收任意大小的请求体
                options.Limits.MaxRequestBodySize = null;

                // 请求缓冲区用于临时存储请求数据 取消缓冲区大小的限制
                options.Limits.MaxRequestBufferSize = null;
            });
        }

        private void ConfigureExceptionHandling()
        {
            // 自定义需要处理的异常
            Configure<AbpProExceptionHandlingOptions>(options =>
            {
                //  加入需要处理的异常类型
                options.Handlers.Add<Volo.Abp.Data.AbpDbConcurrencyException>();
                options.Handlers.Add<AbpInitializationException>();
                options.Handlers.Add<OutOfMemoryException>();
                options.Handlers.Add<System.Data.Common.DbException>();
                options.Handlers.Add<Microsoft.EntityFrameworkCore.DbUpdateException>();
                options.Handlers.Add<System.Data.DBConcurrencyException>();
            });
            // 自定义需要发送邮件通知的异常类型
            //Configure<AbpEmailExceptionHandlingOptions>(options =>
            //{
            //    // 是否发送堆栈信息
            //    options.SendStackTrace = true;
            //    // 未指定异常接收者的默认接收邮件
            //    // 指定自己的邮件地址
            //});

            Configure<VoloAbpExceptionHandlingOptions>(options =>
            {
                options.SendStackTraceToClients = false;
                options.SendExceptionsDetailsToClients = false;
            });
        }

        private void ConfigureVirtualFileSystem()
        {
            Configure<AbpVirtualFileSystemOptions>(options =>
            {
                options.FileSets.AddEmbedded<RuichenAbpProAdminHttpApiHostModule>();
            });
        }

        private void ConfigureUrls(IConfiguration configuration)
        {
            Configure<AppUrlOptions>(options =>
            {
                var applicationConfiguration = configuration.GetSection("App:Urls:Applications");
                foreach (var appConfig in applicationConfiguration.GetChildren())
                {
                    options.Applications[appConfig.Key].RootUrl = appConfig["RootUrl"];
                    foreach (var urlsConfig in appConfig.GetSection("Urls").GetChildren())
                    {
                        options.Applications[appConfig.Key].Urls[urlsConfig.Key] = urlsConfig.Value;
                    }
                }
            });
        }

        private void ConfigureCaching(IConfiguration configuration)
        {
            Configure<AbpDistributedCacheOptions>(options =>
            {
                configuration.GetSection("DistributedCache").Bind(options);
            });

            Configure<RedisCacheOptions>(options =>
            {
                var redisConfig = ConfigurationOptions.Parse(options.Configuration);
                options.ConfigurationOptions = redisConfig;
                options.InstanceName = configuration["Redis:InstanceName"];
            });
        }

        private void ConfigureIdentity(IConfiguration configuration)
        {
            // 增加配置文件定义,在新建租户时需要
            Configure<IdentityOptions>(options =>
            {
                var identityConfiguration = configuration.GetSection("Identity");
                if (identityConfiguration.Exists())
                {
                    identityConfiguration.Bind(options);
                }
            });
            Configure<AbpClaimsPrincipalFactoryOptions>(options =>
            {
                options.IsDynamicClaimsEnabled = true;
            });
        }

        /// <summary>
        /// OpenIddict 配置选项
        /// </summary>
        /// <param name="configuration"></param>
        private void ConfigureAuthServer(IConfiguration configuration)
        {
            Configure<OpenIddictServerAspNetCoreOptions>(options =>
            {
                // 禁用了对传输安全要求的检查。默认情况下，OpenIddict 要求使用 HTTPS（传输层安全性）来保护与授权服务器的通信
                options.DisableTransportSecurityRequirement = true;
            });
        }

        /// <summary>
        /// Swagger 配置选项
        /// </summary>
        /// <param name="services"></param>
        private void ConfigureSwagger(IServiceCollection services)
        {
            // Swagger
            services.AddSwaggerGen(
                options =>
                {
                    // 设置 Swagger 文档的名称和版本
                    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Admin API", Version = "v1" });

                    // 文档包含谓词的过滤器，总是返回 true，意味着包含所有文档
                    options.DocInclusionPredicate((docName, description) => true);

                    // 自定义模式 ID 生成规则，使用类型的全名作为模式 ID
                    options.CustomSchemaIds(type => type.FullName);

                    var assemblies = AppDomain.CurrentDomain.GetAssemblies().Where(item => item.FullName.Contains("RuiChen")).ToArray();

                    // 遍历当前程序集中加载的所有模块
                    foreach (var assembly in assemblies)
                    {
                        // 根据程序集名称生成 XML 文件名
                        var xmlFile = $"{assembly.GetName().Name}.xml";

                        // 生成 XML 文件的完整路径
                        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);

                        // 检查 XML 文件是否存在，存在则添加到 Swagger 配置中
                        if (File.Exists(xmlPath))
                        {
                            options.IncludeXmlComments(xmlPath, true);
                        }
                    }

                    // 定义 Bearer 认证方案
                    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                    {
                        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
                        Name = "Authorization",
                        In = ParameterLocation.Header,
                        Scheme = "bearer",
                        Type = SecuritySchemeType.Http,
                        BearerFormat = "JWT"
                    });

                    // 添加安全要求到 Swagger 文档，要求所有请求都需要 Bearer 认证
                    options.AddSecurityRequirement(new OpenApiSecurityRequirement
                    {
                        {
                            new OpenApiSecurityScheme
                            {
                                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                            },
                            new string[] { }
                        }
                    });

                    // 添加操作过滤器，用于向每个操作添加特定的头参数
                    options.OperationFilter<TenantHeaderParamter>();
                });
        }

        /// <summary>
        /// 配置 API 版本控制和 ABP 框架的 MVC 选项。它通过 AddAbpApiVersioning 方法设置了 API 版本控制的行为，
        /// 并通过 ConfigureAbp 方法将 ABP 框架的预配置应用到 MVC 选项中。这种配置方式有助于确保 API 的版本管理和框架配置的一致性
        /// 为 API 提供版本控制支持，并且确保 MVC 设置能够与 ABP 框架的需求保持一致
        /// </summary>
        /// <param name="services"></param>
        private void ConfigureEndpoints(IServiceCollection services)
        {
            var preActions = services.GetPreConfigureActions<AbpAspNetCoreMvcOptions>();

            services.AddAbpApiVersioning(options =>
            {
                options.ReportApiVersions = true;
                options.AssumeDefaultVersionWhenUnspecified = true;
            },
            mvcOptions =>
            {
                mvcOptions.ConfigureAbp(preActions.Configure());
            });
        }

        /// <summary>
        /// 多租户 配置选项
        /// 
        /// </summary>
        /// <param name="configuration"></param>
        private void ConfigureMultiTenancy(IConfiguration configuration)
        {
            // 启用多租户
            Configure<AbpMultiTenancyOptions>(options =>
            {
                options.IsEnabled = true;
            });

            // 域名解析租户：通过配置域名来解析租户，应用程序可以根据不同的请求域名来确定当前请求属于哪个租户。
            // 这通常用于根据域名进行租户隔离，例如不同的公司或组织访问同一应用程序但使用不同的子域名
            // "App": {"Domains": ["contoso.myapp.com","fabrikam.myapp.com"]}
            // 当用户通过 contoso.myapp.com 访问应用程序时，系统会根据请求的域名 contoso.myapp.com 确定当前请求属于 Contoso 租户。
            // 类似地，如果用户通过 fabrikam.myapp.com 访问应用程序，系统会识别为 Fabrikam 租户。
            var tenantResolveCfg = configuration.GetSection("App:Domains");
            if (tenantResolveCfg.Exists())
            {
                Configure<AbpTenantResolveOptions>(options =>
                {
                    var domains = tenantResolveCfg.Get<string[]>();
                    foreach (var domain in domains)
                    {
                        options.AddDomainTenantResolver(domain);
                    }
                });
            }
        }

        /// <summary>
        /// Json 序列化配置选项
        /// </summary>
        /// <param name="configuration"></param>
        private void ConfigureJsonSerializer(IConfiguration configuration)
        {
            // 统一时间日期格式
            Configure<AbpJsonOptions>(options =>
            {
                var jsonConfiguration = configuration.GetSection("Json");
                if (jsonConfiguration.Exists())
                {
                    jsonConfiguration.Bind(options);
                }
            });
            // 中文序列化的编码问题
            Configure<AbpSystemTextJsonSerializerOptions>(options =>
            {
                options.JsonSerializerOptions.Encoder = JavaScriptEncoder.Create(UnicodeRanges.All);
            });
        }

        private void ConfigureFeatureManagement(IConfiguration configuration)
        {
            if (configuration.GetValue<bool>("FeatureManagement:IsDynamicStoreEnabled"))
            {
                Configure<FeatureManagementOptions>(options =>
                {
                    options.IsDynamicFeatureStoreEnabled = true;
                });
            }
            Configure<FeatureManagementOptions>(options =>
            {
                options.ProviderPolicies[EditionFeatureValueProvider.ProviderName] = AbpSaasPermissions.Editions.ManageFeatures;
                options.ProviderPolicies[TenantFeatureValueProvider.ProviderName] = AbpSaasPermissions.Tenants.ManageFeatures;
            });
        }

        private void ConfigureSettingManagement(IConfiguration configuration)
        {
            if (configuration.GetValue<bool>("SettingManagement:IsDynamicStoreEnabled"))
            {
                Configure<SettingManagementOptions>(options =>
                {
                    options.IsDynamicSettingStoreEnabled = true;
                });
            }
        }

        private void ConfigurePermissionManagement(IConfiguration configuration)
        {
            if (configuration.GetValue<bool>("PermissionManagement:IsDynamicStoreEnabled"))
            {
                Configure<PermissionManagementOptions>(options =>
                {
                    options.IsDynamicPermissionStoreEnabled = true;
                });
            }
            Configure<PermissionManagementOptions>(options =>
            {
                // Rename IdentityServer.Client.ManagePermissions
                // See https://github.com/abpframework/abp/blob/dev/modules/identityserver/src/Volo.Abp.PermissionManagement.Domain.IdentityServer/Volo/Abp/PermissionManagement/IdentityServer/AbpPermissionManagementDomainIdentityServerModule.cs
                options.ProviderPolicies[ClientPermissionValueProvider.ProviderName] = AbpProOpenIddictPermissions.Applications.ManagePermissions;
            });
        }

        private void ConfigureCors(IServiceCollection services, IConfiguration configuration)
        {
            services.AddCors(options =>
            {
                options.AddPolicy(DefaultCorsPolicyName, builder =>
                {
                    builder
                        .WithOrigins(
                            configuration["App:CorsOrigins"]
                                .Split(",", StringSplitOptions.RemoveEmptyEntries)
                                .Select(o => o.RemovePostFix("/"))
                                .ToArray()
                        )
                        .WithAbpExposedHeaders()
                        .WithAbpWrapExposedHeaders()
                        .SetIsOriginAllowedToAllowWildcardSubdomains()
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials();
                });
            });
        }


        private void ConfigureDistributedLock(IServiceCollection services, IConfiguration configuration)
        {
            var distributedLockEnabled = configuration["DistributedLock:IsEnabled"];
            if (distributedLockEnabled.IsNullOrEmpty() || bool.Parse(distributedLockEnabled))
            {
                var redis = ConnectionMultiplexer.Connect(configuration["DistributedLock:Redis:Configuration"]);
                services.AddSingleton<IDistributedLockProvider>(_ => new RedisDistributedSynchronizationProvider(redis.GetDatabase()));
            }
        }

        private void ConfigureSecurity(IServiceCollection services, IConfiguration configuration, bool isDevelopment = false)
        {
            services.AddAuthentication()
                    .AddJwtBearer(options =>
                    {
                        options.Authority = configuration["AuthServer:Authority"];
                        options.RequireHttpsMetadata = false;
                        options.Audience = configuration["AuthServer:Scope"];
                        options.Events = new JwtBearerEvents
                        {
                            OnMessageReceived = context =>
                            {
                                var accessToken = context.Request.Query["access_token"];
                                var path = context.HttpContext.Request.Path;
                                if (!string.IsNullOrEmpty(accessToken) &&
                                    (path.StartsWithSegments("/api/files")))
                                {
                                    context.Token = accessToken;
                                }
                                return Task.CompletedTask;
                            }
                        };
                    });

            if (isDevelopment)
            {
                services.AddAlwaysAllowAuthorization();
            }

            if (!isDevelopment)
            {
                var redis = ConnectionMultiplexer.Connect(configuration["Redis:Configuration"]);
                services
                    .AddDataProtection()
                    .SetApplicationName("RuiChen.AbpPro.Application")
                    .PersistKeysToStackExchangeRedis(redis, "RuiChen.AbpPro.Application:DataProtection:Protection-Keys");
            }

            services.AddSameSiteCookiePolicy();
        }

    }
}
