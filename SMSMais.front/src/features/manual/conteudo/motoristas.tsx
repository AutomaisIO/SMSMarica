import { Truck } from 'lucide-react';
import { Callout } from '@/features/manual/components/Callout';
import { ListaDefinicoes, P } from '@/features/manual/components/Prosa';
import { Passos } from '@/features/manual/components/Passos';
import { AbaRef, BotaoRef, SeloRef } from '@/features/manual/components/Referencia';
import type { Artigo } from '@/features/manual/tipos';

/**
 * Artigo do cadastro de Motoristas do Transporte de Pacientes.
 *
 * Conferido no código: `features/motoristas` (MotoristasPage, MotoristaDetalhePage,
 * FormularioMotorista — passo do CPF + nascimento, promoção de usuário existente, abas Dados
 * pessoais / Motorista / Permissões), `shared/ui/SegurancaSecao`, `features/translados`
 * (TransladoFormPage filtra motorista ativo), `features/tratamentos/components/PainelConfirmacao` e
 * no backend (`MotoristasService` — CNH única, papel único, exclusão lógica que leva a conta junto —,
 * `MotoristasController`, `GeradorDeTransladoService`).
 */
export const artigoMotoristas: Artigo = {
  slug: 'motoristas',
  titulo: 'Motoristas',
  resumo:
    'O cadastro de quem dirige no transporte de pacientes: dados conferidos pelo CPF, CNH, a conta de acesso e onde o motorista aparece nas rotas.',
  grupo: 'transporte',
  icone: Truck,
  rota: '/app/motoristas',
  publico: 'Quem organiza o transporte de pacientes e cadastra a equipe',
  atualizadoEm: '2026-09-30',
  palavrasChave: [
    'motorista',
    'agente de transporte sanitário',
    'condutor',
    'CNH',
    'CPF',
    'data de nascimento',
    'cadastrar motorista',
    'promover',
    'usuário existente',
    'papel',
    'senha',
    'acesso',
    'permissões',
    'perfil',
    'rota',
    'translado',
    'ida',
    'volta',
    'excluir',
    'inativo',
  ],
  secoes: () => [
    {
      id: 'para-que-serve',
      titulo: 'Quem dirige',
      busca: 'motorista agente de transporte sanitário usuário conta de acesso rota translado ida volta',
      conteudo: (
        <>
          <P>
            O motorista é o agente de transporte sanitário que leva o paciente. No sistema ele é
            também um <strong>usuário</strong>: tem conta de acesso, senha e permissões, como
            qualquer pessoa da equipe — o papel dele é “motorista”.
          </P>
          <P>
            É deste cadastro que saem os nomes para escolher o motorista de uma rota, para a geração
            automática distribuir as rotas do dia e para registrar quem dirigiu na ida e na volta de
            cada viagem.
          </P>
        </>
      ),
    },
    {
      id: 'cadastrar',
      titulo: 'Cadastrar um motorista',
      busca: 'novo motorista cadastrar cpf data de nascimento continuar nome conferido cnh dados pessoais permissões abas',
      conteudo: (
        <>
          <Passos
            itens={[
              {
                titulo: (
                  <>
                    Em <strong>Transporte Pacientes → Motoristas</strong>, clique em{' '}
                    <BotaoRef>Novo motorista</BotaoRef>.
                  </>
                ),
              },
              {
                titulo: (
                  <>
                    Informe o CPF e a data de nascimento e clique em <BotaoRef>Continuar</BotaoRef>.
                  </>
                ),
                detalhe:
                  'O sistema confere o par e traz o nome. Nome, CPF e nascimento não se digitam nem se editam depois.',
              },
              {
                titulo: (
                  <>
                    Na aba <AbaRef>Dados pessoais</AbaRef>, complete e-mail, telefone, endereço e foto.
                  </>
                ),
              },
              {
                titulo: (
                  <>
                    Na aba <AbaRef>Motorista</AbaRef>, informe a CNH.
                  </>
                ),
                detalhe: 'Obrigatória, e não pode repetir a de outro motorista.',
              },
              {
                titulo: (
                  <>
                    Na aba <AbaRef>Permissões</AbaRef>, escolha os perfis — o que ele pode ver quando
                    entrar no sistema — e clique em <BotaoRef>Cadastrar</BotaoRef>.
                  </>
                ),
              },
            ]}
          />
          <Callout tipo="regra" titulo="CPF que já está no sistema">
            Se o CPF já é de um usuário sem papel, o formulário vira{' '}
            <strong>Promover usuário existente a motorista</strong>: só se informa a CNH, e os dados
            pessoais continuam os que ele já tinha. Se o CPF já é de um motorista, ou de alguém com
            outro papel (médico, por exemplo), o cadastro é recusado — cada pessoa tem um papel só.
          </Callout>
        </>
      ),
    },
    {
      id: 'acesso',
      titulo: 'Conta de acesso e senha',
      busca: 'senha acesso gerar senha definir senha segurança primeiro acesso trocar e-mail interno',
      conteudo: (
        <>
          <P>
            O motorista nasce <strong>sem senha</strong> que funcione. Para ele entrar, abra{' '}
            <BotaoRef variante="ghost">Editar</BotaoRef> na lista e, na aba{' '}
            <AbaRef>Dados pessoais</AbaRef>, use a seção <strong>Segurança</strong>:
          </P>
          <ListaDefinicoes
            itens={[
              {
                termo: <BotaoRef variante="outline">Gerar nova senha aleatória</BotaoRef>,
                descricao: 'O sistema cria a senha e mostra para você passar ao motorista. Ele é obrigado a trocar no primeiro acesso.',
              },
              {
                termo: <BotaoRef variante="outline">Aplicar nova senha</BotaoRef>,
                descricao:
                  'Você digita a senha (mínimo de 8 caracteres). A troca no primeiro acesso só é exigida se marcar “Exigir que o usuário troque a senha no próximo login”.',
              },
            ]}
          />
          <P>
            Sem e-mail informado, a conta fica com um endereço interno, só para existir.
          </P>
        </>
      ),
    },
    {
      id: 'lista-detalhe',
      titulo: 'Lista e detalhe',
      busca: 'lista nome foto cpf status ativo inativo detalhe cnh telefone translados recentes editar na lista',
      conteudo: (
        <>
          <ListaDefinicoes
            itens={[
              { termo: 'Nome', descricao: 'Com a foto. Clicar abre o detalhe.' },
              { termo: 'CPF', descricao: 'O documento conferido no cadastro.' },
              {
                termo: 'Status',
                descricao: (
                  <>
                    <SeloRef cor="sucesso">Ativo</SeloRef> quando a conta de acesso está liberada.
                    Motorista inativo não aparece para ser escolhido numa rota nem na confirmação da
                    viagem.
                  </>
                ),
              },
            ]}
          />
          <P>
            O detalhe mostra CPF, CNH, telefone, a data do cadastro e os{' '}
            <strong>translados recentes</strong> daquele motorista. Para mudar dados, use{' '}
            <BotaoRef variante="outline">Editar na lista</BotaoRef>.
          </P>
        </>
      ),
    },
    {
      id: 'nas-rotas',
      titulo: 'Onde o motorista aparece',
      busca: 'translado rota montar motorista da rota geração automática distribui confirmar realização motorista da ida volta',
      conteudo: (
        <ListaDefinicoes
          itens={[
            {
              termo: 'Translado montado à mão',
              descricao: 'Ao criar a rota do dia, escolhe-se o veículo e o motorista — só os ativos aparecem.',
            },
            {
              termo: 'Geração automática',
              descricao: 'Distribui os motoristas cadastrados entre as rotas do dia; quem monta confere antes de confirmar.',
            },
            {
              termo: 'Confirmar realização da viagem',
              descricao: 'No atendimento, registra-se quem dirigiu na ida e quem dirigiu na volta.',
            },
          ]}
        />
      ),
    },
    {
      id: 'excluir',
      titulo: 'Excluir',
      busca: 'excluir motorista desligado histórico rotas conta de acesso',
      conteudo: (
        <>
          <P>
            <BotaoRef variante="danger">Excluir</BotaoRef> tira o motorista das listas e encerra a
            conta de acesso dele junto. As rotas e viagens que ele fez continuam registradas.
          </P>
          <Callout tipo="atencao" titulo="Motorista excluído não se edita">
            Depois de excluído, o cadastro fica como estava. Se a pessoa voltar, cadastre de novo.
          </Callout>
        </>
      ),
    },
    {
      id: 'quem-pode',
      titulo: 'Quem pode o quê',
      busca: 'permissão perfil módulo motoristas consulta inclusão edição exclusão',
      conteudo: (
        <>
          <P>
            A permissão é <strong>Motoristas</strong>, na tela de Perfis:
          </P>
          <ListaDefinicoes
            itens={[
              { termo: 'Consulta', descricao: 'Ver a lista e o detalhe.' },
              { termo: 'Inclusão', descricao: 'Cadastrar motorista e promover usuário existente.' },
              { termo: 'Edição', descricao: 'Editar dados, CNH, senha e permissões.' },
              { termo: 'Exclusão', descricao: 'Excluir.' },
            ]}
          />
        </>
      ),
    },
    {
      id: 'duvidas',
      titulo: 'Dúvidas frequentes',
      busca: 'cpf já cadastrado outro papel cnh repetida não aparece na rota nome errado',
      conteudo: (
        <ListaDefinicoes
          itens={[
            {
              termo: '“CPF já cadastrado como …”',
              descricao: 'A pessoa já tem outro papel no sistema (médico, por exemplo). Uma pessoa tem um papel só.',
            },
            {
              termo: '“Já existe motorista com esta CNH.”',
              descricao: 'Outro motorista ativo usa essa CNH. Confira o número.',
            },
            {
              termo: 'O motorista não aparece para escolher na rota.',
              descricao: 'A conta dele está inativa, ou ele foi excluído.',
            },
            {
              termo: 'O nome veio diferente do que a equipe chama.',
              descricao: 'O nome vem conferido pelo CPF e não se edita — é o nome do documento.',
            },
          ]}
        />
      ),
    },
  ],
};
