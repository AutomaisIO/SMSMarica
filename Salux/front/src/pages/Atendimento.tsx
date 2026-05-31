import { useEffect, useState } from "react";
import {
  api,
  type BaaHeader,
  type EdocResumo,
  type FiaHeader,
} from "../api";

type Props = { tipo: "F" | "B"; nr: number; ano: number };
type Aba = "header" | "edocs" | "sinais";

export function Atendimento({ tipo, nr, ano }: Props) {
  const [aba, setAba] = useState<Aba>("header");
  const [erro, setErro] = useState<string | null>(null);
  const [fia, setFia] = useState<FiaHeader | null>(null);
  const [baa, setBaa] = useState<BaaHeader | null>(null);

  useEffect(() => {
    setErro(null);
    setFia(null);
    setBaa(null);
    if (tipo === "F") {
      api.fia(nr, ano).then(setFia).catch((e) => setErro(String(e)));
    } else {
      api.baa(nr, ano).then(setBaa).catch((e) => setErro(String(e)));
    }
  }, [tipo, nr, ano]);

  if (erro) return <div className="error">{erro}</div>;
  if (tipo === "F" && !fia) return <div className="empty">carregando FIA {nr}/{ano}…</div>;
  if (tipo === "B" && !baa) return <div className="empty">carregando BAA {nr}/{ano}…</div>;

  const titulo = tipo === "F" ? `FIA ${nr}/${ano}` : `BAA ${nr}/${ano}`;

  return (
    <>
      <h2>{titulo} — {(fia?.nm_paciente ?? baa?.nm_paciente) ?? ""}</h2>
      <nav className="tabs">
        <button className={aba === "header" ? "active" : ""} onClick={() => setAba("header")}>
          Cabeçalho
        </button>
        <button className={aba === "edocs" ? "active" : ""} onClick={() => setAba("edocs")}>
          eDocs
        </button>
        {tipo === "F" && (
          <button className={aba === "sinais" ? "active" : ""} onClick={() => setAba("sinais")}>
            Sinais vitais
          </button>
        )}
      </nav>

      {aba === "header" && (tipo === "F" ? <AbaFia f={fia!} /> : <AbaBaa b={baa!} />)}
      {aba === "edocs" && <AbaEdocs tipo={tipo} nr={nr} ano={ano} />}
      {aba === "sinais" && tipo === "F" && <AbaSinais nr={nr} ano={ano} />}
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

function AbaFia({ f }: { f: FiaHeader }) {
  return (
    <div className="grid">
      <Linha rotulo="Paciente" valor={f.nm_paciente} />
      <Linha rotulo="Internação" valor={f.id_internacao === "U" ? "Urgência" : f.id_internacao === "E" ? "Eletiva" : f.id_internacao} />
      <Linha rotulo="Baixa" valor={f.dt_baixa} />
      <Linha rotulo="Alta" valor={f.dt_alta} />
      <Linha rotulo="Previsão alta" valor={f.dt_previsao_alta} />
      <Linha rotulo="Dias internado" valor={f.nr_dias_internacao} />
      <Linha rotulo="Leito atual" valor={f.leito_atual} />
      <Linha rotulo="Médico entrada" valor={f.nm_medico_entrada} />
      <Linha rotulo="Médico alta" valor={f.nm_medico_alta} />
      <Linha rotulo="CID" valor={f.cd_cid} />
      <Linha rotulo="Responsável" valor={f.nm_responsavel} />
      <Linha rotulo="CPF resp." valor={f.nm_responsavel ? undefined : undefined} />
    </div>
  );
}

function AbaBaa({ b }: { b: BaaHeader }) {
  return (
    <div className="grid">
      <Linha rotulo="Paciente" valor={b.nm_paciente} />
      <Linha rotulo="Atendimento" valor={b.dt_atendimento} />
      <Linha rotulo="Chegada" valor={b.dt_chegada} />
      <Linha rotulo="Saída" valor={b.dt_saida} />
      <Linha rotulo="Emergência" valor={b.in_emergencia} />
      <Linha rotulo="Destino" valor={b.id_destino} />
      <Linha rotulo="Atendido" valor={b.in_baa_atendido} />
      <Linha rotulo="Especialidade" valor={b.ds_especialidade} />
      <Linha rotulo="Setor" valor={b.ds_setor} />
      <Linha rotulo="Senha" valor={b.nro_senha} />
      <Linha rotulo="Médico" valor={b.nm_medico} />
      <Linha rotulo="Classif. risco" valor={b.ds_classif_risco} />
      <Linha rotulo="CID" valor={b.cd_cid} />
    </div>
  );
}

function AbaEdocs({ tipo, nr, ano }: { tipo: "F" | "B"; nr: number; ano: number }) {
  const [linhas, setLinhas] = useState<EdocResumo[] | null>(null);
  const [erro, setErro] = useState<string | null>(null);
  const [aberto, setAberto] = useState<EdocResumo | null>(null);

  useEffect(() => {
    setLinhas(null);
    setAberto(null);
    const p = tipo === "F" ? api.edocsFia(nr, ano) : api.edocsBaa(nr, ano);
    p.then((r) => setLinhas(r.edocs)).catch((e) => setErro(String(e)));
  }, [tipo, nr, ano]);

  if (erro) return <div className="error">{erro}</div>;
  if (!linhas) return <div className="empty">carregando eDocs…</div>;
  if (!linhas.length) return <div className="empty">sem eDocs preenchidos.</div>;

  if (aberto) {
    return (
      <>
        <p style={{ marginBottom: "0.5rem" }}>
          <a onClick={() => setAberto(null)} style={{ color: "#c00", cursor: "pointer" }}>
            ← voltar à lista
          </a>
        </p>
        <h3>{aberto.ds_modelo}</h3>
        <div style={{ fontSize: 13, color: "#666", marginBottom: "1rem" }}>
          Movimento {aberto.id_movimento} · {aberto.dt_inclusao} · {aberto.nm_funcionario ?? "?"} · status {aberto.in_status}
        </div>
        <EdocConteudo cd_hospital={1} ano={ano} id={Number(aberto.id_movimento)} />
      </>
    );
  }

  return (
    <table>
      <thead>
        <tr>
          <th>Modelo</th>
          <th>Grupo</th>
          <th>Categoria</th>
          <th>Inclusão</th>
          <th>Por</th>
          <th>Status</th>
        </tr>
      </thead>
      <tbody>
        {linhas.map((e) => (
          <tr key={e.id_movimento} onClick={() => setAberto(e)}>
            <td>{e.ds_modelo}</td>
            <td>{e.grupo ?? "—"}</td>
            <td>{e.categoria ?? "—"}</td>
            <td>{e.dt_inclusao}</td>
            <td>{e.nm_funcionario ?? "—"}</td>
            <td>{e.in_status === "D" ? "Definitivo" : e.in_status === "P" ? "Parcial" : e.in_status}</td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}

function EdocConteudo({ cd_hospital, ano, id }: { cd_hospital: number; ano: number; id: number }) {
  const [itens, setItens] = useState<Array<{ ds_item?: string | null; tipo?: string | null; ds_resposta?: string | null }> | null>(null);
  const [erro, setErro] = useState<string | null>(null);
  useEffect(() => {
    setItens(null);
    api.edocMovimento(cd_hospital, ano, id).then((r) => setItens(r.itens)).catch((e) => setErro(String(e)));
  }, [cd_hospital, ano, id]);
  if (erro) return <div className="error">{erro}</div>;
  if (!itens) return <div className="empty">carregando…</div>;
  if (!itens.length) return <div className="empty">sem itens.</div>;
  return (
    <div className="grid">
      {itens.map((it, i) => (
        <Linha key={i} rotulo={it.ds_item ?? "(sem label)"} valor={it.ds_resposta} />
      ))}
    </div>
  );
}

function AbaSinais({ nr, ano }: { nr: number; ano: number }) {
  const [linhas, setLinhas] = useState<Array<{
    dthr_visita: string; peso?: string | null; altura?: string | null;
    temp?: string | null; pa_sist?: string | null; pa_diast?: string | null;
    fc?: string | null; fr?: string | null; spo2?: string | null; glicemia?: string | null;
  }> | null>(null);
  const [erro, setErro] = useState<string | null>(null);
  useEffect(() => {
    setLinhas(null);
    api.sinaisVitais(nr, ano).then((r) => setLinhas(r["aferições"])).catch((e) => setErro(String(e)));
  }, [nr, ano]);
  if (erro) return <div className="error">{erro}</div>;
  if (!linhas) return <div className="empty">carregando…</div>;
  if (!linhas.length) return <div className="empty">sem aferições.</div>;
  return (
    <table>
      <thead>
        <tr>
          <th>Data/hora</th>
          <th>PA</th>
          <th>FC</th>
          <th>FR</th>
          <th>SpO₂</th>
          <th>Temp</th>
          <th>Glic</th>
          <th>Peso</th>
        </tr>
      </thead>
      <tbody>
        {linhas.map((s, i) => (
          <tr key={i}>
            <td>{s.dthr_visita}</td>
            <td>{s.pa_sist}/{s.pa_diast}</td>
            <td>{s.fc}</td>
            <td>{s.fr}</td>
            <td>{s.spo2}</td>
            <td>{s.temp}</td>
            <td>{s.glicemia}</td>
            <td>{s.peso}</td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}
