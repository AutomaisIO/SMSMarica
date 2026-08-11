using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMarica.Data.Migrations
{
    /// <inheritdoc />
    public partial class PesquisaPerfilDeQuemClica : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "paciente_nascimento",
                schema: "smsmarica",
                table: "pesquisa_satisfacao",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "paciente_sexo",
                schema: "smsmarica",
                table: "pesquisa_satisfacao",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "paciente_nascimento",
                schema: "smsmarica",
                table: "pesquisa_satisfacao");

            migrationBuilder.DropColumn(
                name: "paciente_sexo",
                schema: "smsmarica",
                table: "pesquisa_satisfacao");
        }
    }
}
