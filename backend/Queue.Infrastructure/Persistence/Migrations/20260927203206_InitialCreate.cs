using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Queue.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "app_roles",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_app_roles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "queue_daily_sequences",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    queue_date = table.Column<DateOnly>(type: "date", nullable: false),
                    service_id = table.Column<long>(type: "bigint", nullable: false),
                    prefix = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    current_number = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    number_length = table.Column<int>(type: "integer", nullable: false, defaultValue: 3),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_queue_daily_sequences", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "queue_services",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    prefix = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    number_length = table.Column<int>(type: "integer", nullable: false, defaultValue: 3),
                    priority = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    estimated_service_minutes = table.Column<int>(type: "integer", nullable: false, defaultValue: 5),
                    skip_line_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    display_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_queue_services", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "queue_settings",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    value = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    value_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    is_system = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_queue_settings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "app_user_roles",
                columns: table => new
                {
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    role_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_app_user_roles", x => new { x.user_id, x.role_id });
                    table.ForeignKey(
                        name: "fk_app_user_roles_app_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "app_roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "app_users",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    display_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    email = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    counter_id = table.Column<long>(type: "bigint", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    last_login_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_login_ip = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    failed_login_count = table.Column<int>(type: "integer", nullable: false),
                    lockout_end_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_app_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "queue_counters",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    service_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    service_id = table.Column<long>(type: "bigint", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    current_ticket_id = table.Column<long>(type: "bigint", nullable: true),
                    weight = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    display_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_queue_counters", x => x.id);
                    table.ForeignKey(
                        name: "fk_queue_counters_services_service_id",
                        column: x => x.service_id,
                        principalTable: "queue_services",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "queue_tickets",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    queue_date = table.Column<DateOnly>(type: "date", nullable: false),
                    ticket_no = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    prefix = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    sequence_no = table.Column<int>(type: "integer", nullable: false),
                    service_id = table.Column<long>(type: "bigint", nullable: false),
                    counter_id = table.Column<long>(type: "bigint", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    priority = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    queue_position = table.Column<long>(type: "bigint", nullable: false),
                    customer_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    customer_phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    qr_token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    called_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    serving_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cancelled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    no_show_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    call_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    remark = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_queue_tickets", x => x.id);
                    table.ForeignKey(
                        name: "fk_queue_tickets_queue_counters_counter_id",
                        column: x => x.counter_id,
                        principalTable: "queue_counters",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_queue_tickets_queue_services_service_id",
                        column: x => x.service_id,
                        principalTable: "queue_services",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "queue_ticket_histories",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ticket_id = table.Column<long>(type: "bigint", nullable: false),
                    from_status = table.Column<int>(type: "integer", nullable: true),
                    to_status = table.Column<int>(type: "integer", nullable: false),
                    counter_id = table.Column<long>(type: "bigint", nullable: true),
                    operator_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    operator_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    action = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ip_address = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    user_agent = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    remark = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_queue_ticket_histories", x => x.id);
                    table.ForeignKey(
                        name: "fk_queue_ticket_histories_queue_tickets_ticket_id",
                        column: x => x.ticket_id,
                        principalTable: "queue_tickets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "app_roles",
                columns: new[] { "id", "code", "description", "name" },
                values: new object[,]
                {
                    { 1L, "Admin", "使用者、角色、服務、櫃台、佇列、系統設定、統計", "系統管理員" },
                    { 2L, "Manager", "佇列、櫃台、統計、部分設定", "管理者" },
                    { 3L, "Counter", "叫號、再叫、開始、完成、過號、轉移", "櫃台服務人員" },
                    { 4L, "Display", "讀取佇列狀態、接收 SignalR 事件", "叫號顯示器" },
                    { 5L, "Kiosk", "取號、讀取服務", "自助取號設備" }
                });

            migrationBuilder.InsertData(
                table: "app_users",
                columns: new[] { "id", "counter_id", "created_at", "display_name", "email", "failed_login_count", "is_active", "last_login_at", "last_login_ip", "lockout_end_at", "password_hash", "updated_at", "user_name" },
                values: new object[,]
                {
                    { 1L, null, new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "系統管理員", "admin@queue.local", 0, true, null, null, null, "210000.prHx6RnMQ/c9Qi6+80IedA==.rCnw2+H1R2dVAL3XUvPXzKBYXhNyo/xsh+aBXWmTBOw=", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "admin" },
                    { 3L, null, new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Kiosk 服務", null, 0, true, null, null, null, "210000.asZbnO3AHduCrzY6mLzToA==.5CGPtI6RSYR1tHob0Sdvzr0hwsU9AFQW/BwPpxn22os=", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "kiosk" }
                });

            migrationBuilder.InsertData(
                table: "queue_services",
                columns: new[] { "id", "code", "created_at", "description", "display_order", "estimated_service_minutes", "is_active", "name", "number_length", "prefix", "updated_at" },
                values: new object[] { 1L, "GENERAL", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "一般業務辦理", 1, 5, true, "一般服務", 3, "A", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) });

            migrationBuilder.InsertData(
                table: "queue_services",
                columns: new[] { "id", "code", "created_at", "description", "display_order", "estimated_service_minutes", "is_active", "name", "number_length", "prefix", "priority", "skip_line_enabled", "updated_at" },
                values: new object[] { 2L, "VIP", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "VIP 貴賓專屬服務，優先叫號", 2, 10, true, "VIP 服務", 3, "V", 100, true, new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) });

            migrationBuilder.InsertData(
                table: "queue_services",
                columns: new[] { "id", "code", "created_at", "description", "display_order", "estimated_service_minutes", "is_active", "name", "number_length", "prefix", "priority", "updated_at" },
                values: new object[] { 3L, "APPOINTMENT", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "已預約客戶報到專用", 3, 8, true, "預約報到", 3, "R", 50, new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) });

            migrationBuilder.InsertData(
                table: "queue_services",
                columns: new[] { "id", "code", "created_at", "description", "display_order", "estimated_service_minutes", "is_active", "name", "number_length", "prefix", "priority", "skip_line_enabled", "updated_at" },
                values: new object[] { 4L, "EXPRESS", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "急件/插單服務", 4, 3, true, "急件處理", 3, "E", 200, true, new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) });

            migrationBuilder.InsertData(
                table: "queue_settings",
                columns: new[] { "id", "category", "created_at", "description", "is_system", "key", "updated_at", "value", "value_type" },
                values: new object[,]
                {
                    { 1L, "Queue", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "營業時間（每日重新編號起訖）", true, "queue.business_hours", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "08:00-17:00", "string" },
                    { 2L, "Queue", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "叫號後等待逾時秒數", true, "queue.recall_timeout_seconds", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "180", "int" },
                    { 3L, "Queue", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "同一票據最大再叫次數，超過自動過號", true, "queue.max_recall_count", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "2", "int" },
                    { 4L, "Queue", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "全站平均服務分鐘數（預估等待時間）", true, "queue.average_service_minutes", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "5", "int" },
                    { 5L, "Queue", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "是否啟用等待老化優先權加成（§17.2）", true, "queue.aging_enabled", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "false", "bool" },
                    { 6L, "Queue", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "等待老化因子：每 N 分鐘 +1 優先權", true, "queue.aging_factor", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "10", "int" },
                    { 7L, "Display", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "叫號語音播放", true, "display.voice_enabled", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "true", "bool" },
                    { 8L, "Display", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "語音語言（Web Speech API）", true, "display.voice_language", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "zh-TW", "string" },
                    { 9L, "Display", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "廣告輪播間隔秒數", true, "display.rotation_seconds", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "10", "int" },
                    { 10L, "Display", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "顯示器廣告輪播內容（以 ｜ 分隔）", true, "display.ads", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "歡迎光臨服務大廳｜請留意叫號資訊", "string" },
                    { 11L, "Security", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "QR Code Token 有效小時數", true, "security.qr_token_lifetime_hours", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "72", "int" },
                    { 12L, "Kiosk", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Kiosk 取號畫面自動重置秒數", true, "kiosk.auto_refresh_seconds", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "10", "int" }
                });

            migrationBuilder.InsertData(
                table: "app_user_roles",
                columns: new[] { "role_id", "user_id" },
                values: new object[,]
                {
                    { 1L, 1L },
                    { 2L, 1L },
                    { 5L, 3L }
                });

            migrationBuilder.InsertData(
                table: "queue_counters",
                columns: new[] { "id", "code", "created_at", "current_ticket_id", "display_order", "is_active", "name", "service_code", "service_id", "status", "updated_at", "weight" },
                values: new object[,]
                {
                    { 1L, "1", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 1, true, "1 號櫃台", "GENERAL", 1L, 1, new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1 },
                    { 2L, "2", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 2, true, "2 號櫃台", "VIP", 2L, 1, new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1 },
                    { 3L, "3", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 3, true, "3 號櫃台", "APPOINTMENT", 3L, 1, new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1 },
                    { 4L, "4", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 4, true, "4 號櫃台（VIP）", "VIP", 2L, 1, new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1 },
                    { 5L, "5", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 5, true, "5 號櫃台（預約）", "APPOINTMENT", 3L, 1, new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1 }
                });

            migrationBuilder.InsertData(
                table: "app_users",
                columns: new[] { "id", "counter_id", "created_at", "display_name", "email", "failed_login_count", "is_active", "last_login_at", "last_login_ip", "lockout_end_at", "password_hash", "updated_at", "user_name" },
                values: new object[] { 2L, 1L, new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "櫃台服務人員 1", null, 0, true, null, null, null, "210000.Fe1nLmiqK0P0MqQma/BPeA==.+HLJhyKdsN5whTnM4GSoetT0ZcvWHJJBn3MNusEX6IA=", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "counter1" });

            migrationBuilder.InsertData(
                table: "app_user_roles",
                columns: new[] { "role_id", "user_id" },
                values: new object[] { 3L, 2L });

            migrationBuilder.CreateIndex(
                name: "ux_app_roles_code",
                table: "app_roles",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_app_user_roles_role_id",
                table: "app_user_roles",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "ix_app_users_counter",
                table: "app_users",
                column: "counter_id");

            migrationBuilder.CreateIndex(
                name: "ux_app_users_username",
                table: "app_users",
                column: "user_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_queue_counters_current_ticket_id",
                table: "queue_counters",
                column: "current_ticket_id");

            migrationBuilder.CreateIndex(
                name: "ix_queue_counters_service_id",
                table: "queue_counters",
                column: "service_id");

            migrationBuilder.CreateIndex(
                name: "ix_queue_counters_status",
                table: "queue_counters",
                columns: new[] { "is_active", "status" });

            migrationBuilder.CreateIndex(
                name: "ux_queue_counters_code",
                table: "queue_counters",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_queue_daily_sequence_date_service",
                table: "queue_daily_sequences",
                columns: new[] { "queue_date", "service_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_queue_services_active",
                table: "queue_services",
                columns: new[] { "is_active", "display_order" });

            migrationBuilder.CreateIndex(
                name: "ux_queue_services_code",
                table: "queue_services",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_queue_services_prefix",
                table: "queue_services",
                column: "prefix",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_queue_settings_key",
                table: "queue_settings",
                column: "key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_queue_history_action",
                table: "queue_ticket_histories",
                columns: new[] { "action", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_queue_history_ticket",
                table: "queue_ticket_histories",
                columns: new[] { "ticket_id", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_queue_ticket_counter_status",
                table: "queue_tickets",
                columns: new[] { "counter_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_queue_ticket_date_status",
                table: "queue_tickets",
                columns: new[] { "queue_date", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_queue_ticket_service_status",
                table: "queue_tickets",
                columns: new[] { "service_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ux_queue_ticket_date_prefix_sequence",
                table: "queue_tickets",
                columns: new[] { "queue_date", "prefix", "sequence_no" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_app_user_roles_app_users_user_id",
                table: "app_user_roles",
                column: "user_id",
                principalTable: "app_users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_app_users_counters_counter_id",
                table: "app_users",
                column: "counter_id",
                principalTable: "queue_counters",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_queue_counters_tickets_current_ticket_id",
                table: "queue_counters",
                column: "current_ticket_id",
                principalTable: "queue_tickets",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_queue_tickets_queue_counters_counter_id",
                table: "queue_tickets");

            migrationBuilder.DropTable(
                name: "app_user_roles");

            migrationBuilder.DropTable(
                name: "queue_daily_sequences");

            migrationBuilder.DropTable(
                name: "queue_settings");

            migrationBuilder.DropTable(
                name: "queue_ticket_histories");

            migrationBuilder.DropTable(
                name: "app_roles");

            migrationBuilder.DropTable(
                name: "app_users");

            migrationBuilder.DropTable(
                name: "queue_counters");

            migrationBuilder.DropTable(
                name: "queue_tickets");

            migrationBuilder.DropTable(
                name: "queue_services");
        }
    }
}
