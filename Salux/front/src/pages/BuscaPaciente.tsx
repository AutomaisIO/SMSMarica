import { useState, type FormEvent } from "react";
import { api, type Paciente } from "../api";

type Props = { onSelecionar: (cd: number) => void };

export function BuscaPaciente({ onSelecionar }: Props) {
  const [nome, setNome] = useState("");
  const [cpf, setCpf] = useState("");
  const [resultados, setResultados] = useState<Paciente[]>([]);
  const [carregando, setCarregando] = useState(false);
  const [erro, setErro] = useState<string | null>(null);

  async function buscar(e?: FormEvent) {
    e?.preventDefault();
    if (!nome && !cpf) return;
    setCarregando(true);
    setErro(null);
    try {
      const { resultados } = await api.buscarPaciente(nome, cpf);
      setResultados(resultados);
    } catch (e) {
      setErro(String(e));
    } finally {
      setCarregando(false);
    }
  }

  return (
    <>
      <h2>Buscar paciente</h2>
      <form className="busca" onSubmit={buscar}>
        <input
          autoFocus
          placeholder="Nome (parte é o suficiente)"
          value={nome}
          onChange={(e) => setNome(e.target.value)}
        />
        <input
          placeholder="ou CPF"
          value={cpf}
          onChange={(e) => setCpf(e.target.value)}
          style={{ maxWidth: 160 }}
        />
        <button type="submit" disabled={carregando || (!nome && !cpf)}>
          {carregando ? "buscando…" : "buscar"}
        </button>
      </form>

      {erro && <div className="error">{erro}</div>}

      {resultados.length > 0 ? (
        <table>
          <thead>
            <tr>
              <th>CD</th>
              <th>Nome</th>
              <th>Nasc</th>
              <th>Sexo</th>
              <th>CPF</th>
              <th>CNS</th>
              <th>Ativo</th>
            </tr>
          </thead>
          <tbody>
            {resultados.map((p) => (
              <tr key={p.cd_paciente} onClick={() => onSelecionar(Number(p.cd_paciente))}>
                <td>{p.cd_paciente}</td>
                <td>{p.nm_paciente}</td>
                <td>{p.dt_nascimento ?? "—"}</td>
                <td>{p.sexo ?? "—"}</td>
                <td>{p.cpf_paciente ?? "—"}</td>
                <td>{p.cns ?? "—"}</td>
                <td>{p.in_ativo ?? "—"}</td>
              </tr>
            ))}
          </tbody>
        </table>
      ) : (
        !carregando && (nome || cpf) && <div className="empty">Nenhum resultado.</div>
      )}
    </>
  );
}
