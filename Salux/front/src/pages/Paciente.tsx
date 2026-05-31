import { useEffect, useState } from "react";
import { api, type CadastroPaciente, type Atendimento } from "../api";

type Props = {
  cd: number;
  onAbrirAtendimento: (tipo: "F" | "B", nr: number, ano: number) => void;
};
type Aba = "cadastro" | "historico" | "alergias";

export function Paciente({ cd, onAbrirAtendimento }: Props) {
  const [aba, setAba] = useState<Aba>("cadastro");
  const [cadastro, setCadastro] = useState<CadastroPaciente | null>(null);
  const [erro, setErro] = useState<string | null>(null);

  useEffect(() => {
    setCadastro(null);
    api.cadastro(cd).then(setCadastro).catch((e) => setErro(String(e)));
  }, [cd]);

  if (erro) return <div className="error">{erro}</div>;
  if (!cadastro) return <div className="empty">carregando paciente {cd}…</div>;

  return (
    <>
      <h2>{cadastro.nm_paciente}</h2>
      <nav className="tabs">
        <button className={aba === "cadastro" ? "active" : ""} onClick={() => setAba("cadastro")}>
          Cadastro
        </button>
        <button className={aba === "historico" ? "active" : ""} onClick={() => setAba("historico")}>
          Histórico
        </button>
        <button className={aba === "alergias" ? "active" : ""} onClick={() => setAba("alergias")}>
          Alergias
        </button>
      </nav>

      {aba === "cadastro" && <AbaCadastro p={cadastro} />}
      {aba === "historico" && <AbaHistorico cd={cd} onAbrir={onAbrirAtendimento} />}
      {aba === "alergias" && <AbaAlergias cd={cd} />}
    </>
  );
}

function Linha({ rotulo, valor }: { rotulo: string; valor?: string | null }) {
  return (
    <>
      <div className="k">{rotulo}</div>
      <div className={"v" + (valor ? "" : " empty-v")}>{valor || "—"}</div>
    </>
  );
}

function AbaCadastro({ p }: { p: CadastroPaciente }) {
  return (
    <>
      <h3>Identificação</h3>
      <div className="grid">
        <Linha rotulo="CD Paciente" valor={p.cd_paciente} />
        <Linha rotulo="Apelido" valor={p.sc_apelido} />
        <Linha rotulo="Sexo" valor={p.sexo} />
        <Linha rotulo="Nascimento" valor={p.dt_nascimento} />
        <Linha rotulo="CPF" valor={p.cpf_paciente} />
        <Linha rotulo="RG" valor={p.rg_paciente} />
        <Linha rotulo="CNS" valor={p.cns} />
        <Linha rotulo="Estado civil" valor={p.estado_civil} />
        <Linha rotulo="Ativo" valor={p.in_ativo} />
        <Linha rotulo="Data óbito" valor={p.dt_obito} />
      </div>

      <h3>Filiação</h3>
      <div className="grid">
        <Linha rotulo="Mãe" valor={p.nm_mae} />
        <Linha rotulo="Pai" valor={p.nm_pai} />
        <Linha rotulo="Cônjuge" valor={p.nm_conjuge} />
      </div>

      <h3>Endereço</h3>
      <div className="grid">
        <Linha rotulo="Logradouro" valor={p.nm_logradouro} />
        <Linha rotulo="Número" valor={p.nr_logradouro} />
        <Linha rotulo="Complemento" valor={p.compl_logradouro} />
        <Linha rotulo="Bairro" valor={p.bairro} />
        <Linha rotulo="Cidade" valor={p.nm_cidade} />
        <Linha rotulo="UF" valor={p.cd_uf} />
        <Linha rotulo="CEP" valor={p.cep} />
      </div>

      <h3>Contato</h3>
      <div className="grid">
        <Linha rotulo="Telefone" valor={[p.nr_ddd_fone, p.nr_fone].filter(Boolean).join(" ")} />
        <Linha rotulo="Complemento" valor={p.nr_fone_compl} />
        <Linha rotulo="E-mail" valor={p.email} />
      </div>

      <h3>Outros</h3>
      <div className="grid">
        <Linha rotulo="Profissão" valor={p.profissao} />
        <Linha rotulo="Religião" valor={p.religiao} />
        <Linha rotulo="Peso" valor={p.peso} />
        <Linha rotulo="Altura" valor={p.altura} />
        <Linha rotulo="Prontuário SGH" valor={p.cd_pront_sgh} />
        <Linha rotulo="Prontuário CEM" valor={p.cd_pront_cem} />
        <Linha rotulo="Data cadastro" valor={p.dt_cadastro} />
      </div>
    </>
  );
}

function AbaHistorico({
  cd,
  onAbrir,
}: {
  cd: number;
  onAbrir: (tipo: "F" | "B", nr: number, ano: number) => void;
}) {
  const [linhas, setLinhas] = useState<Atendimento[] | null>(null);
  const [erro, setErro] = useState<string | null>(null);
  useEffect(() => {
    setLinhas(null);
    api.historico(cd).then((r) => setLinhas(r.atendimentos)).catch((e) => setErro(String(e)));
  }, [cd]);
  if (erro) return <div className="error">{erro}</div>;
  if (!linhas) return <div className="empty">carregando…</div>;
  if (!linhas.length) return <div className="empty">sem atendimentos.</div>;
  return (
    <table>
      <thead>
        <tr>
          <th>Tipo</th>
          <th>Nº</th>
          <th>Entrada</th>
          <th>Alta</th>
          <th>Médico</th>
          <th>Clínica/Esp.</th>
          <th>CID</th>
        </tr>
      </thead>
      <tbody>
        {linhas.map((a, i) => (
          <tr
            key={i}
            onClick={() => onAbrir(a.id_tipo, Number(a.nr_doc), Number(a.dt_ano))}
          >
            <td>
              <span className={"tag-" + a.id_tipo}>{a.id_tipo === "F" ? "FIA" : "BAA"}</span>
            </td>
            <td>{a.nr_doc}/{a.dt_ano}</td>
            <td>{a.dt_entrada}</td>
            <td>{a.dt_alta ?? "—"}</td>
            <td>{a.nm_medico ?? "—"}</td>
            <td>{a.ds_clinica ?? "—"}</td>
            <td>{a.cd_cid ?? "—"}</td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}

function AbaAlergias({ cd }: { cd: number }) {
  const [linhas, setLinhas] = useState<Array<{ ds_alergia: string; ds_material?: string | null; ds_descritivo?: string | null }> | null>(null);
  const [erro, setErro] = useState<string | null>(null);
  useEffect(() => {
    setLinhas(null);
    api.alergias(cd).then((r) => setLinhas(r.alergias)).catch((e) => setErro(String(e)));
  }, [cd]);
  if (erro) return <div className="error">{erro}</div>;
  if (!linhas) return <div className="empty">carregando…</div>;
  if (!linhas.length) return <div className="empty">sem alergias registradas.</div>;
  return (
    <table>
      <thead>
        <tr><th>Alergia</th><th>Medicamento</th><th>Descritivo</th></tr>
      </thead>
      <tbody>
        {linhas.map((a, i) => (
          <tr key={i}>
            <td>{a.ds_alergia}</td>
            <td>{a.ds_material ?? "—"}</td>
            <td>{a.ds_descritivo ?? "—"}</td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}
