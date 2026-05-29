using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMarica.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFhirSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "fhir");

            migrationBuilder.CreateTable(
                name: "barreira_comunicacao",
                schema: "fhir",
                columns: table => new
                {
                    codigo = table.Column<int>(type: "integer", nullable: false),
                    nome = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_barreira_comunicacao", x => x.codigo);
                });

            migrationBuilder.CreateTable(
                name: "cbo_ocupacao",
                schema: "fhir",
                columns: table => new
                {
                    codigo = table.Column<int>(type: "integer", nullable: false),
                    titulo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cbo_ocupacao", x => x.codigo);
                });

            migrationBuilder.CreateTable(
                name: "etnia_indigena",
                schema: "fhir",
                columns: table => new
                {
                    codigo = table.Column<int>(type: "integer", nullable: false),
                    nome = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_etnia_indigena", x => x.codigo);
                });

            migrationBuilder.CreateTable(
                name: "municipio_ibge",
                schema: "fhir",
                columns: table => new
                {
                    codigo = table.Column<int>(type: "integer", nullable: false),
                    nome = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    uf = table.Column<string>(type: "character(2)", fixedLength: true, maxLength: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_municipio_ibge", x => x.codigo);
                });

            migrationBuilder.CreateTable(
                name: "organization",
                schema: "fhir",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_id = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    last_updated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    alias = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    part_of_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organization", x => x.id);
                    table.ForeignKey(
                        name: "FK_organization_organization_part_of_id",
                        column: x => x.part_of_id,
                        principalSchema: "fhir",
                        principalTable: "organization",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pais_iso",
                schema: "fhir",
                columns: table => new
                {
                    codigo_alfa3 = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    codigo_alfa2 = table.Column<string>(type: "character(2)", fixedLength: true, maxLength: 2, nullable: false),
                    nome = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pais_iso", x => x.codigo_alfa3);
                });

            migrationBuilder.CreateTable(
                name: "practitioner",
                schema: "fhir",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_id = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    last_updated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    gender = table.Column<int>(type: "integer", nullable: false),
                    birth_date = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_practitioner", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "religiao",
                schema: "fhir",
                columns: table => new
                {
                    codigo = table.Column<int>(type: "integer", nullable: false),
                    nome = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_religiao", x => x.codigo);
                });

            migrationBuilder.CreateTable(
                name: "organization_identifier",
                schema: "fhir",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    system = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    value = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    use = table.Column<int>(type: "integer", nullable: false),
                    period_start = table.Column<DateOnly>(type: "date", nullable: true),
                    period_end = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organization_identifier", x => x.id);
                    table.ForeignKey(
                        name: "FK_organization_identifier_organization_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "fhir",
                        principalTable: "organization",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "practitioner_address",
                schema: "fhir",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    practitioner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    use = table.Column<int>(type: "integer", nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    text = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    line1 = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    line2 = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    district = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    municipio_codigo = table.Column<int>(type: "integer", nullable: true),
                    state = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    postal_code = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    country = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    period_start = table.Column<DateOnly>(type: "date", nullable: true),
                    period_end = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_practitioner_address", x => x.id);
                    table.ForeignKey(
                        name: "FK_practitioner_address_municipio_ibge_municipio_codigo",
                        column: x => x.municipio_codigo,
                        principalSchema: "fhir",
                        principalTable: "municipio_ibge",
                        principalColumn: "codigo",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_practitioner_address_practitioner_practitioner_id",
                        column: x => x.practitioner_id,
                        principalSchema: "fhir",
                        principalTable: "practitioner",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "practitioner_identifier",
                schema: "fhir",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    practitioner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    system = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    value = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    use = table.Column<int>(type: "integer", nullable: false),
                    period_start = table.Column<DateOnly>(type: "date", nullable: true),
                    period_end = table.Column<DateOnly>(type: "date", nullable: true),
                    issuer_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    issuer_state = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_practitioner_identifier", x => x.id);
                    table.ForeignKey(
                        name: "FK_practitioner_identifier_practitioner_practitioner_id",
                        column: x => x.practitioner_id,
                        principalSchema: "fhir",
                        principalTable: "practitioner",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "practitioner_name",
                schema: "fhir",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    practitioner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    use = table.Column<int>(type: "integer", nullable: false),
                    text = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    family = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    given = table.Column<string[]>(type: "text[]", nullable: false),
                    prefix = table.Column<string[]>(type: "text[]", nullable: false),
                    suffix = table.Column<string[]>(type: "text[]", nullable: false),
                    period_start = table.Column<DateOnly>(type: "date", nullable: true),
                    period_end = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_practitioner_name", x => x.id);
                    table.ForeignKey(
                        name: "FK_practitioner_name_practitioner_practitioner_id",
                        column: x => x.practitioner_id,
                        principalSchema: "fhir",
                        principalTable: "practitioner",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "practitioner_qualification",
                schema: "fhir",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    practitioner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    council_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    council_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    council_state = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    specialty_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    specialty_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    period_start = table.Column<DateOnly>(type: "date", nullable: true),
                    period_end = table.Column<DateOnly>(type: "date", nullable: true),
                    issuer_organization_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_practitioner_qualification", x => x.id);
                    table.ForeignKey(
                        name: "FK_practitioner_qualification_organization_issuer_organization~",
                        column: x => x.issuer_organization_id,
                        principalSchema: "fhir",
                        principalTable: "organization",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_practitioner_qualification_practitioner_practitioner_id",
                        column: x => x.practitioner_id,
                        principalSchema: "fhir",
                        principalTable: "practitioner",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "practitioner_telecom",
                schema: "fhir",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    practitioner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    system = table.Column<int>(type: "integer", nullable: false),
                    value = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    use = table.Column<int>(type: "integer", nullable: false),
                    rank = table.Column<int>(type: "integer", nullable: true),
                    period_start = table.Column<DateOnly>(type: "date", nullable: true),
                    period_end = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_practitioner_telecom", x => x.id);
                    table.ForeignKey(
                        name: "FK_practitioner_telecom_practitioner_practitioner_id",
                        column: x => x.practitioner_id,
                        principalSchema: "fhir",
                        principalTable: "practitioner",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "patient",
                schema: "fhir",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_id = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    last_updated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    gender = table.Column<int>(type: "integer", nullable: false),
                    birth_date = table.Column<DateOnly>(type: "date", nullable: true),
                    birth_date_estimated = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    deceased_boolean = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    deceased_datetime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deceased_presumed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    marital_status = table.Column<int>(type: "integer", nullable: false),
                    multiple_birth_boolean = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    multiple_birth_integer = table.Column<int>(type: "integer", nullable: true),
                    managing_organization_id = table.Column<Guid>(type: "uuid", nullable: true),
                    race = table.Column<int>(type: "integer", nullable: false),
                    etnia_indigena_codigo = table.Column<int>(type: "integer", nullable: true),
                    mothers_maiden_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    fathers_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    birth_country_code = table.Column<string>(type: "character(3)", maxLength: 3, nullable: true),
                    birth_municipio_codigo = table.Column<int>(type: "integer", nullable: true),
                    country_entry_date = table.Column<DateOnly>(type: "date", nullable: true),
                    religiao_codigo = table.Column<int>(type: "integer", nullable: true),
                    ocupacao_cbo_codigo = table.Column<int>(type: "integer", nullable: true),
                    education_level = table.Column<int>(type: "integer", nullable: false),
                    attends_school = table.Column<bool>(type: "boolean", nullable: true),
                    gender_identity = table.Column<int>(type: "integer", nullable: false),
                    use_social_name = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    has_no_documentation = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    notes = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<Guid>(type: "uuid", nullable: true),
                    legal_basis = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_patient", x => x.id);
                    table.ForeignKey(
                        name: "FK_patient_cbo_ocupacao_ocupacao_cbo_codigo",
                        column: x => x.ocupacao_cbo_codigo,
                        principalSchema: "fhir",
                        principalTable: "cbo_ocupacao",
                        principalColumn: "codigo",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_patient_etnia_indigena_etnia_indigena_codigo",
                        column: x => x.etnia_indigena_codigo,
                        principalSchema: "fhir",
                        principalTable: "etnia_indigena",
                        principalColumn: "codigo",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_patient_municipio_ibge_birth_municipio_codigo",
                        column: x => x.birth_municipio_codigo,
                        principalSchema: "fhir",
                        principalTable: "municipio_ibge",
                        principalColumn: "codigo",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_patient_organization_managing_organization_id",
                        column: x => x.managing_organization_id,
                        principalSchema: "fhir",
                        principalTable: "organization",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_patient_pais_iso_birth_country_code",
                        column: x => x.birth_country_code,
                        principalSchema: "fhir",
                        principalTable: "pais_iso",
                        principalColumn: "codigo_alfa3",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_patient_religiao_religiao_codigo",
                        column: x => x.religiao_codigo,
                        principalSchema: "fhir",
                        principalTable: "religiao",
                        principalColumn: "codigo",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "consent",
                schema: "fhir",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_id = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    last_updated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    legal_basis = table.Column<int>(type: "integer", nullable: false),
                    granted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    valid_from = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    valid_until = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    grantor_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    document_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_consent", x => x.id);
                    table.ForeignKey(
                        name: "FK_consent_patient_patient_id",
                        column: x => x.patient_id,
                        principalSchema: "fhir",
                        principalTable: "patient",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "patient_address",
                schema: "fhir",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    use = table.Column<int>(type: "integer", nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    text = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    line1 = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    line2 = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    district = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    municipio_codigo = table.Column<int>(type: "integer", nullable: true),
                    state = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    postal_code = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    country = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    reference_point = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    latitude = table.Column<decimal>(type: "numeric(10,7)", precision: 10, scale: 7, nullable: true),
                    longitude = table.Column<decimal>(type: "numeric(10,7)", precision: 10, scale: 7, nullable: true),
                    period_start = table.Column<DateOnly>(type: "date", nullable: true),
                    period_end = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_patient_address", x => x.id);
                    table.ForeignKey(
                        name: "FK_patient_address_municipio_ibge_municipio_codigo",
                        column: x => x.municipio_codigo,
                        principalSchema: "fhir",
                        principalTable: "municipio_ibge",
                        principalColumn: "codigo",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_patient_address_patient_patient_id",
                        column: x => x.patient_id,
                        principalSchema: "fhir",
                        principalTable: "patient",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "patient_communication",
                schema: "fhir",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    language_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    preferred = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    barreira_comunicacao_codigo = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_patient_communication", x => x.id);
                    table.ForeignKey(
                        name: "FK_patient_communication_barreira_comunicacao_barreira_comunic~",
                        column: x => x.barreira_comunicacao_codigo,
                        principalSchema: "fhir",
                        principalTable: "barreira_comunicacao",
                        principalColumn: "codigo",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_patient_communication_patient_patient_id",
                        column: x => x.patient_id,
                        principalSchema: "fhir",
                        principalTable: "patient",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "patient_contact",
                schema: "fhir",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    relationship = table.Column<int>(type: "integer", nullable: false),
                    relationship_text = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    telephone_ddd = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: true),
                    telephone_number = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    gender = table.Column<int>(type: "integer", nullable: false),
                    address_line = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    address_city = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    address_state = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    address_postal_code = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    period_start = table.Column<DateOnly>(type: "date", nullable: true),
                    period_end = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_patient_contact", x => x.id);
                    table.ForeignKey(
                        name: "FK_patient_contact_patient_patient_id",
                        column: x => x.patient_id,
                        principalSchema: "fhir",
                        principalTable: "patient",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "patient_disability",
                schema: "fhir",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    description = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    cid_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    start_date = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_patient_disability", x => x.id);
                    table.ForeignKey(
                        name: "FK_patient_disability_patient_patient_id",
                        column: x => x.patient_id,
                        principalSchema: "fhir",
                        principalTable: "patient",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "patient_identifier",
                schema: "fhir",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    system = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    value = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    use = table.Column<int>(type: "integer", nullable: false),
                    period_start = table.Column<DateOnly>(type: "date", nullable: true),
                    period_end = table.Column<DateOnly>(type: "date", nullable: true),
                    issuer_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    issuer_state = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    registry_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    registry_book = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    registry_page = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    registry_term = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    assigner_organization_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_patient_identifier", x => x.id);
                    table.ForeignKey(
                        name: "FK_patient_identifier_organization_assigner_organization_id",
                        column: x => x.assigner_organization_id,
                        principalSchema: "fhir",
                        principalTable: "organization",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_patient_identifier_patient_patient_id",
                        column: x => x.patient_id,
                        principalSchema: "fhir",
                        principalTable: "patient",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "patient_link",
                schema: "fhir",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    other_patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_patient_link", x => x.id);
                    table.ForeignKey(
                        name: "FK_patient_link_patient_other_patient_id",
                        column: x => x.other_patient_id,
                        principalSchema: "fhir",
                        principalTable: "patient",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_patient_link_patient_patient_id",
                        column: x => x.patient_id,
                        principalSchema: "fhir",
                        principalTable: "patient",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "patient_name",
                schema: "fhir",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    use = table.Column<int>(type: "integer", nullable: false),
                    text = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    family = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    given = table.Column<string[]>(type: "text[]", nullable: false),
                    prefix = table.Column<string[]>(type: "text[]", nullable: false),
                    suffix = table.Column<string[]>(type: "text[]", nullable: false),
                    period_start = table.Column<DateOnly>(type: "date", nullable: true),
                    period_end = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_patient_name", x => x.id);
                    table.ForeignKey(
                        name: "FK_patient_name_patient_patient_id",
                        column: x => x.patient_id,
                        principalSchema: "fhir",
                        principalTable: "patient",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "patient_photo",
                schema: "fhir",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    content_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    data_base64 = table.Column<string>(type: "text", nullable: true),
                    url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    authorized_display = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_patient_photo", x => x.id);
                    table.ForeignKey(
                        name: "FK_patient_photo_patient_patient_id",
                        column: x => x.patient_id,
                        principalSchema: "fhir",
                        principalTable: "patient",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "patient_telecom",
                schema: "fhir",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    system = table.Column<int>(type: "integer", nullable: false),
                    value = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    use = table.Column<int>(type: "integer", nullable: false),
                    rank = table.Column<int>(type: "integer", nullable: true),
                    period_start = table.Column<DateOnly>(type: "date", nullable: true),
                    period_end = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_patient_telecom", x => x.id);
                    table.ForeignKey(
                        name: "FK_patient_telecom_patient_patient_id",
                        column: x => x.patient_id,
                        principalSchema: "fhir",
                        principalTable: "patient",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_cbo_ocupacao_titulo",
                schema: "fhir",
                table: "cbo_ocupacao",
                column: "titulo");

            migrationBuilder.CreateIndex(
                name: "ix_consent_patient_type_status",
                schema: "fhir",
                table: "consent",
                columns: new[] { "patient_id", "type", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_municipio_ibge_nome",
                schema: "fhir",
                table: "municipio_ibge",
                column: "nome");

            migrationBuilder.CreateIndex(
                name: "ix_municipio_ibge_uf",
                schema: "fhir",
                table: "municipio_ibge",
                column: "uf");

            migrationBuilder.CreateIndex(
                name: "ix_organization_deleted_at",
                schema: "fhir",
                table: "organization",
                column: "deleted_at",
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_organization_part_of_id",
                schema: "fhir",
                table: "organization",
                column: "part_of_id");

            migrationBuilder.CreateIndex(
                name: "IX_organization_identifier_organization_id",
                schema: "fhir",
                table: "organization_identifier",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ux_organization_identifier_system_value",
                schema: "fhir",
                table: "organization_identifier",
                columns: new[] { "system", "value" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_pais_iso_codigo_alfa2",
                schema: "fhir",
                table: "pais_iso",
                column: "codigo_alfa2",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_patient_birth_country_code",
                schema: "fhir",
                table: "patient",
                column: "birth_country_code");

            migrationBuilder.CreateIndex(
                name: "IX_patient_birth_municipio_codigo",
                schema: "fhir",
                table: "patient",
                column: "birth_municipio_codigo");

            migrationBuilder.CreateIndex(
                name: "ix_patient_deleted_at",
                schema: "fhir",
                table: "patient",
                column: "deleted_at",
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_patient_etnia_indigena_codigo",
                schema: "fhir",
                table: "patient",
                column: "etnia_indigena_codigo");

            migrationBuilder.CreateIndex(
                name: "IX_patient_managing_organization_id",
                schema: "fhir",
                table: "patient",
                column: "managing_organization_id");

            migrationBuilder.CreateIndex(
                name: "IX_patient_ocupacao_cbo_codigo",
                schema: "fhir",
                table: "patient",
                column: "ocupacao_cbo_codigo");

            migrationBuilder.CreateIndex(
                name: "IX_patient_religiao_codigo",
                schema: "fhir",
                table: "patient",
                column: "religiao_codigo");

            migrationBuilder.CreateIndex(
                name: "IX_patient_address_municipio_codigo",
                schema: "fhir",
                table: "patient_address",
                column: "municipio_codigo");

            migrationBuilder.CreateIndex(
                name: "ix_patient_address_patient_id",
                schema: "fhir",
                table: "patient_address",
                column: "patient_id");

            migrationBuilder.CreateIndex(
                name: "IX_patient_communication_barreira_comunicacao_codigo",
                schema: "fhir",
                table: "patient_communication",
                column: "barreira_comunicacao_codigo");

            migrationBuilder.CreateIndex(
                name: "ix_patient_communication_patient_id",
                schema: "fhir",
                table: "patient_communication",
                column: "patient_id");

            migrationBuilder.CreateIndex(
                name: "ix_patient_contact_patient_id",
                schema: "fhir",
                table: "patient_contact",
                column: "patient_id");

            migrationBuilder.CreateIndex(
                name: "ix_patient_disability_patient_id",
                schema: "fhir",
                table: "patient_disability",
                column: "patient_id");

            migrationBuilder.CreateIndex(
                name: "IX_patient_identifier_assigner_organization_id",
                schema: "fhir",
                table: "patient_identifier",
                column: "assigner_organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_patient_identifier_patient_id",
                schema: "fhir",
                table: "patient_identifier",
                column: "patient_id");

            migrationBuilder.CreateIndex(
                name: "ux_patient_identifier_system_value",
                schema: "fhir",
                table: "patient_identifier",
                columns: new[] { "system", "value" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_patient_link_other_patient_id",
                schema: "fhir",
                table: "patient_link",
                column: "other_patient_id");

            migrationBuilder.CreateIndex(
                name: "ix_patient_link_patient_id",
                schema: "fhir",
                table: "patient_link",
                column: "patient_id");

            migrationBuilder.CreateIndex(
                name: "ix_patient_name_patient_id",
                schema: "fhir",
                table: "patient_name",
                column: "patient_id");

            migrationBuilder.CreateIndex(
                name: "ix_patient_name_text",
                schema: "fhir",
                table: "patient_name",
                column: "text");

            migrationBuilder.CreateIndex(
                name: "ix_patient_photo_patient_id",
                schema: "fhir",
                table: "patient_photo",
                column: "patient_id");

            migrationBuilder.CreateIndex(
                name: "ix_patient_telecom_patient_id",
                schema: "fhir",
                table: "patient_telecom",
                column: "patient_id");

            migrationBuilder.CreateIndex(
                name: "ix_practitioner_deleted_at",
                schema: "fhir",
                table: "practitioner",
                column: "deleted_at",
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_practitioner_address_municipio_codigo",
                schema: "fhir",
                table: "practitioner_address",
                column: "municipio_codigo");

            migrationBuilder.CreateIndex(
                name: "ix_practitioner_address_practitioner_id",
                schema: "fhir",
                table: "practitioner_address",
                column: "practitioner_id");

            migrationBuilder.CreateIndex(
                name: "IX_practitioner_identifier_practitioner_id",
                schema: "fhir",
                table: "practitioner_identifier",
                column: "practitioner_id");

            migrationBuilder.CreateIndex(
                name: "ux_practitioner_identifier_system_value",
                schema: "fhir",
                table: "practitioner_identifier",
                columns: new[] { "system", "value" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_practitioner_name_practitioner_id",
                schema: "fhir",
                table: "practitioner_name",
                column: "practitioner_id");

            migrationBuilder.CreateIndex(
                name: "ix_practitioner_name_text",
                schema: "fhir",
                table: "practitioner_name",
                column: "text");

            migrationBuilder.CreateIndex(
                name: "IX_practitioner_qualification_issuer_organization_id",
                schema: "fhir",
                table: "practitioner_qualification",
                column: "issuer_organization_id");

            migrationBuilder.CreateIndex(
                name: "IX_practitioner_qualification_practitioner_id",
                schema: "fhir",
                table: "practitioner_qualification",
                column: "practitioner_id");

            migrationBuilder.CreateIndex(
                name: "ux_practitioner_qualification_council",
                schema: "fhir",
                table: "practitioner_qualification",
                columns: new[] { "council_code", "council_number", "council_state" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_practitioner_telecom_practitioner_id",
                schema: "fhir",
                table: "practitioner_telecom",
                column: "practitioner_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "consent",
                schema: "fhir");

            migrationBuilder.DropTable(
                name: "organization_identifier",
                schema: "fhir");

            migrationBuilder.DropTable(
                name: "patient_address",
                schema: "fhir");

            migrationBuilder.DropTable(
                name: "patient_communication",
                schema: "fhir");

            migrationBuilder.DropTable(
                name: "patient_contact",
                schema: "fhir");

            migrationBuilder.DropTable(
                name: "patient_disability",
                schema: "fhir");

            migrationBuilder.DropTable(
                name: "patient_identifier",
                schema: "fhir");

            migrationBuilder.DropTable(
                name: "patient_link",
                schema: "fhir");

            migrationBuilder.DropTable(
                name: "patient_name",
                schema: "fhir");

            migrationBuilder.DropTable(
                name: "patient_photo",
                schema: "fhir");

            migrationBuilder.DropTable(
                name: "patient_telecom",
                schema: "fhir");

            migrationBuilder.DropTable(
                name: "practitioner_address",
                schema: "fhir");

            migrationBuilder.DropTable(
                name: "practitioner_identifier",
                schema: "fhir");

            migrationBuilder.DropTable(
                name: "practitioner_name",
                schema: "fhir");

            migrationBuilder.DropTable(
                name: "practitioner_qualification",
                schema: "fhir");

            migrationBuilder.DropTable(
                name: "practitioner_telecom",
                schema: "fhir");

            migrationBuilder.DropTable(
                name: "barreira_comunicacao",
                schema: "fhir");

            migrationBuilder.DropTable(
                name: "patient",
                schema: "fhir");

            migrationBuilder.DropTable(
                name: "practitioner",
                schema: "fhir");

            migrationBuilder.DropTable(
                name: "cbo_ocupacao",
                schema: "fhir");

            migrationBuilder.DropTable(
                name: "etnia_indigena",
                schema: "fhir");

            migrationBuilder.DropTable(
                name: "municipio_ibge",
                schema: "fhir");

            migrationBuilder.DropTable(
                name: "organization",
                schema: "fhir");

            migrationBuilder.DropTable(
                name: "pais_iso",
                schema: "fhir");

            migrationBuilder.DropTable(
                name: "religiao",
                schema: "fhir");
        }
    }
}
