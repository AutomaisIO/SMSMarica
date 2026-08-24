using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveCredencialSisregPorUnidade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sisreg_credencial_unidade",
                schema: "smsmarica");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sisreg_credencial_unidade",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    atualizado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    cnes_confirmado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    criado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    senha_cifrada = table.Column<string>(type: "text", nullable: false),
                    unidade_id = table.Column<Guid>(type: "uuid", nullable: false),
                    unidade_sisreg_nome = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    usuario = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    validado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sisreg_credencial_unidade", x => x.id);
                    table.ForeignKey(
                        name: "FK_sisreg_credencial_unidade_unidade_unidade_id",
                        column: x => x.unidade_id,
                        principalSchema: "smsmarica",
                        principalTable: "unidade",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_sisreg_credencial_unidade",
                schema: "smsmarica",
                table: "sisreg_credencial_unidade",
                column: "unidade_id",
                unique: true);
        }
    }
}
