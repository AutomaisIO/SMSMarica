using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class TrilhaDaAssociacaoNoPacs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "nome_paciente_dicom_original",
                schema: "smsmarica",
                table: "exame_associacao",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "patient_id_dicom_original",
                schema: "smsmarica",
                table: "exame_associacao",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "study_instance_uid_original",
                schema: "smsmarica",
                table: "exame_associacao",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "nome_paciente_dicom_original",
                schema: "smsmarica",
                table: "exame_associacao");

            migrationBuilder.DropColumn(
                name: "patient_id_dicom_original",
                schema: "smsmarica",
                table: "exame_associacao");

            migrationBuilder.DropColumn(
                name: "study_instance_uid_original",
                schema: "smsmarica",
                table: "exame_associacao");
        }
    }
}
