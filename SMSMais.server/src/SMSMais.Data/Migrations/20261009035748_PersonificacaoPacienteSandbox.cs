using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class PersonificacaoPacienteSandbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "personificacao_id",
                schema: "smsmarica",
                table: "cidadao_sessao",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "personificacao_paciente",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    criada_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expira_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    encerrada_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_personificacao_paciente", x => x.id);
                    table.ForeignKey(
                        name: "FK_personificacao_paciente_usuario_usuario_id",
                        column: x => x.usuario_id,
                        principalSchema: "smsmarica",
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_cidadao_sessao_personificacao",
                schema: "smsmarica",
                table: "cidadao_sessao",
                column: "personificacao_id",
                filter: "personificacao_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_personificacao_paciente_usuario_aberta",
                schema: "smsmarica",
                table: "personificacao_paciente",
                column: "usuario_id",
                unique: true,
                filter: "encerrada_em IS NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_cidadao_sessao_personificacao_paciente_personificacao_id",
                schema: "smsmarica",
                table: "cidadao_sessao",
                column: "personificacao_id",
                principalSchema: "smsmarica",
                principalTable: "personificacao_paciente",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_cidadao_sessao_personificacao_paciente_personificacao_id",
                schema: "smsmarica",
                table: "cidadao_sessao");

            migrationBuilder.DropTable(
                name: "personificacao_paciente",
                schema: "smsmarica");

            migrationBuilder.DropIndex(
                name: "ix_cidadao_sessao_personificacao",
                schema: "smsmarica",
                table: "cidadao_sessao");

            migrationBuilder.DropColumn(
                name: "personificacao_id",
                schema: "smsmarica",
                table: "cidadao_sessao");
        }
    }
}
