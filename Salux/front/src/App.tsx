import { useState } from "react";
import { BuscaPaciente } from "./pages/BuscaPaciente";
import { Paciente } from "./pages/Paciente";
import { Atendimento } from "./pages/Atendimento";

type Tela =
  | { kind: "busca" }
  | { kind: "paciente"; cd: number }
  | { kind: "atendimento"; tipo: "F" | "B"; nr: number; ano: number; voltar_cd: number };

export function App() {
  const [tela, setTela] = useState<Tela>({ kind: "busca" });

  return (
    <div className="app">
      <h1>Salux Discovery</h1>
      <div className="crumb">
        <a onClick={() => setTela({ kind: "busca" })}>Busca</a>
        {tela.kind === "paciente" && <> · paciente {tela.cd}</>}
        {tela.kind === "atendimento" && (
          <>
            {" · "}
            <a onClick={() => setTela({ kind: "paciente", cd: tela.voltar_cd })}>
              paciente {tela.voltar_cd}
            </a>
            {" · "}
            {tela.tipo === "F" ? "FIA" : "BAA"} {tela.nr}/{tela.ano}
          </>
        )}
      </div>

      {tela.kind === "busca" && (
        <BuscaPaciente onSelecionar={(cd) => setTela({ kind: "paciente", cd })} />
      )}
      {tela.kind === "paciente" && (
        <Paciente
          cd={tela.cd}
          onAbrirAtendimento={(tipo, nr, ano) =>
            setTela({ kind: "atendimento", tipo, nr, ano, voltar_cd: tela.cd })
          }
        />
      )}
      {tela.kind === "atendimento" && (
        <Atendimento tipo={tela.tipo} nr={tela.nr} ano={tela.ano} />
      )}
    </div>
  );
}
