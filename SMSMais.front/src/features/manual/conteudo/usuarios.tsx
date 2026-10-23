import { UserCog } from 'lucide-react';
import { Callout } from '@/features/manual/components/Callout';
import { Item, Lista, ListaDefinicoes, P } from '@/features/manual/components/Prosa';
import { Passos } from '@/features/manual/components/Passos';
import { AbaRef, BotaoRef } from '@/features/manual/components/Referencia';
import type { Artigo } from '@/features/manual/tipos';

/**
 * Artigo do cadastro de Usuários (contas de acesso ao painel).
 *
 * Conferido no código: `features/usuarios` (UsuariosPage — busca, filtro de unidade com "Sem unidade
 * padrão", Excluir só para ativo; FormularioUsuario — passo do CPF que consulta usuário existente,
 * Receita com nascimento ou cadastro de médico/paciente sem ele; abas Dados/Permissões/Unidades;
 * nome de usuário 3–40 sem ser só números; logins do SISREG; senha inicial; acesso global só para
 * quem o tem e só na edição; unidade padrão obrigatória antes de salvar; UnidadesSecao),
 * `shared/ui/PermissoesSecao`, `shared/ui/SegurancaSecao`, `features/unidades` (aba Usuários do
 * detalhe), `shared/auth/authStore` (unidade ao entrar, permissões recarregadas a cada carga) e no
 * backend `IdentidadeService` (lista sem motoristas; exclusão lógica; motorista exclui pela tela
 * dele; acesso global só por quem tem), `UsuarioUnidadeService` (ADR-0081), `IdentidadeController`
 * (Consulta/Inclusão/Edição/Exclusão) e `RequerPermissaoAttribute` (cache de 30 s).
 */
export const artigoUsuarios: Artigo = {
  slug: 'usuarios',
  titulo: 'Usuários',
  resumo:
    'As contas de quem entra no painel: cadastrar pelo CPF, como a pessoa entra, perfis e permissões, as unidades que ela enxerga, a unidade padrão obrigatória, senha e exclusão.',
  grupo: 'cadastros',
  icone: UserCog,
  rota: '/app/usuarios',
  publico: 'Quem administra o acesso da equipe ao sistema',
  atualizadoEm: '2026-10-23',
  palavrasChave: [
    'usuário',
    'conta',
    'acesso',
    'login',
    'nome de usuário',
    'entrar',
    'e-mail',
    'CPF',
    'senha',
    'trocar senha',
    'senha aleatória',
    'esqueci a senha',
    'perfil',
    'permissões',
    'módulo',
    'unidade',
    'unidade padrão',
    'lotação',
    'sem unidade padrão',
    'vínculo',
    'todas as unidades',
    'acesso global',
    'logins do SISREG',
    'excluir usuário',
    'novo usuário',
    'cadastrar usuário',
    'não vejo nada',
    'menu sumiu',
  ],
  secoes: () => [
    {
      id: 'para-que-serve',
      titulo: 'Quem tem conta aqui',
      busca: 'usuário conta acesso equipe painel motorista médico profissional criar usuário de acesso',
      conteudo: (
        <>
          <P>
            Cada pessoa da equipe que entra no painel tem uma <strong>conta de usuário</strong>: com
            ela vêm a senha, o que a pessoa pode fazer (perfis e permissões) e de quais unidades ela
            enxerga os dados. É nesta tela que se cria, ajusta e exclui essas contas.
          </P>
          <Lista>
            <Item>
              <strong>Motoristas</strong> não aparecem aqui: têm a tela própria em Transporte
              Pacientes → Motoristas.
            </Item>
            <Item>
              <strong>Médicos e demais profissionais</strong> podem ganhar a conta a partir do próprio
              cadastro (<BotaoRef>Criar usuário de acesso</BotaoRef>) — os dados já vêm preenchidos e o
              passo do CPF é pulado. O resto do formulário é o mesmo descrito aqui.
            </Item>
          </Lista>
        </>
      ),
    },
    {
      id: 'cadastrar',
      titulo: 'Cadastrar um usuário',
      busca: 'novo usuário cadastrar cpf data de nascimento continuar receita nome conferido abas dados permissões unidades',
      conteudo: (
        <>
          <Passos
            itens={[
              {
                titulo: (
                  <>
                    Em <strong>Cadastros → Usuários</strong>, clique em <BotaoRef>Novo usuário</BotaoRef>.
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
                  'Com a data de nascimento, o CPF é conferido na Receita e o nome vem de lá. Sem ela, o sistema procura a pessoa no cadastro de médicos e de pacientes; se não achar, pede a data. Se o CPF já tem conta, avisa em vez de criar outra.',
              },
              {
                titulo: (
                  <>
                    Na aba <AbaRef>Dados</AbaRef>, complete e-mail, telefone, endereço, foto e, se
                    quiser, o nome de usuário e a senha inicial.
                  </>
                ),
              },
              {
                titulo: (
                  <>
                    Na aba <AbaRef>Permissões</AbaRef>, escolha os perfis.
                  </>
                ),
              },
              {
                titulo: (
                  <>
                    Na aba <AbaRef>Unidades</AbaRef>, marque as unidades e escolha a{' '}
                    <strong>unidade padrão</strong>.
                  </>
                ),
                detalhe: 'Sem unidade padrão o cadastro não é salvo — veja abaixo por quê.',
              },
              { titulo: <>Clique em <BotaoRef>Cadastrar</BotaoRef>.</> },
            ]}
          />
          <P>
            Nome, CPF e data de nascimento <strong>não se editam depois</strong>: são a identidade
            conferida no começo. O e-mail pode ser incluído ou trocado a qualquer momento — deixá-lo em
            branco na edição não apaga o que já estava.
          </P>
        </>
      ),
    },
    {
      id: 'entrar',
      titulo: 'Como a pessoa entra',
      busca: 'login entrar nome de usuário e-mail cpf mesmo campo senha 8 caracteres trocar no próximo login logins sisreg',
      conteudo: (
        <>
          <P>
            Na tela de entrada há um campo só, que aceita <strong>nome de usuário, e-mail ou CPF</strong>.
            Por isso o nome de usuário tem regras:
          </P>
          <Lista>
            <Item>de 3 a 40 caracteres — letras, números, ponto, hífen ou _;</Item>
            <Item>
              não pode ser <strong>só números</strong> (confundiria com o CPF) nem ter @ (confundiria
              com o e-mail);
            </Item>
            <Item>não diferencia maiúsculas e minúsculas, e não pode repetir o de outra pessoa.</Item>
          </Lista>
          <P>
            A senha tem no mínimo 8 caracteres. Marcando <strong>Exigir troca de senha no próximo
            login</strong>, a pessoa só consegue usar o sistema depois de trocar a senha.
          </P>
          <P>
            <strong>Logins no SISREG</strong> é opcional: um ou mais logins que a pessoa usa no SISREG,
            separados por vírgula. Serve para dar nome ao operador nas estatísticas do SISREG.
          </P>
        </>
      ),
    },
    {
      id: 'permissoes',
      titulo: 'Perfis e permissões',
      busca: 'perfil permissões módulo herdada esmaecida dar a mais override menu sumiu f5 quando vale',
      conteudo: (
        <>
          <P>
            O que a pessoa pode fazer vem dos <strong>perfis</strong> marcados na aba{' '}
            <AbaRef>Permissões</AbaRef> — pode ser mais de um. Os perfis em si se montam em Cadastros →
            Perfis.
          </P>
          <P>
            Abaixo dos perfis fica a matriz de permissões: o que aparece <strong>esmaecido</strong> veio
            de algum perfil; marcar uma caixa a mais dá aquela permissão <strong>só a esta pessoa</strong>,
            sem mexer no perfil.
          </P>
          <Callout tipo="dica" titulo="Mudei a permissão e nada aconteceu">
            O sistema passa a cobrar a permissão nova em até meio minuto. O menu da pessoa, porém, só
            muda quando ela recarrega a página (F5).
          </Callout>
        </>
      ),
    },
    {
      id: 'unidades',
      titulo: 'Unidades e unidade padrão',
      busca:
        'unidades vínculo enxerga dados unidade padrão obrigatória lotação comunicação interna central de atendimento todas as unidades ao entrar seletor do topo',
      conteudo: (
        <>
          <P>A aba <AbaRef>Unidades</AbaRef> responde duas perguntas diferentes:</P>
          <ListaDefinicoes
            itens={[
              {
                termo: 'Unidades marcadas',
                descricao:
                  'De quais unidades a pessoa enxerga os dados — os pedidos, as filas, as listas. Sem nenhuma unidade marcada ela não vê nada (a não ser que tenha acesso global). Quem trabalha para a rede inteira, como a regulação, costuma ter todas.',
              },
              {
                termo: 'Unidade padrão',
                descricao:
                  'Onde a pessoa está lotada. É obrigatória e é uma só. É por ela que a pessoa aparece na Comunicação interna, e é nela que fica a conversa do WhatsApp que a pessoa inicia na Central de Atendimento.',
              },
            ]}
          />
          <P>
            Separar as duas é o que permite, por exemplo, alguém enxergar os pedidos de todas as
            unidades e, no chat interno, aparecer só na equipe em que trabalha — e não na de todas.
          </P>
          <Lista>
            <Item>
              A primeira unidade marcada já vira a padrão; para trocar, use a opção{' '}
              <strong>Unidade padrão</strong> dentro da unidade desejada.
            </Item>
            <Item>Desmarcar a unidade padrão passa a marca para a primeira que sobrar.</Item>
            <Item>O topo da aba mostra qual é a padrão — ou avisa, em amarelo, que falta escolher.</Item>
          </Lista>
          <P>
            <strong>Ao entrar no sistema</strong>, quem tem uma unidade só começa nela; quem tem várias
            começa em “Todas as unidades” e troca no seletor do topo da tela. A unidade padrão não
            decide isso: a lotação de alguém pode ser uma unidade que não atende ninguém.
          </P>
          <Callout tipo="atencao" titulo="Unidade nova não aparece no seletor do topo">
            A lista do seletor é montada quando a pessoa entra. Depois de incluir uma unidade, ela
            precisa <strong>sair e entrar de novo</strong> para vê-la ali.
          </Callout>
        </>
      ),
    },
    {
      id: 'sem-padrao',
      titulo: 'Quem ainda está sem unidade padrão',
      busca: 'sem unidade padrão filtro pendente corrigir não consigo salvar marque a unidade padrão',
      conteudo: (
        <>
          <P>
            Contas antigas podem ter ficado sem unidade padrão. Para achá-las, no filtro{' '}
            <strong>Unidade</strong> da lista escolha <strong>— Sem unidade padrão —</strong> e clique em{' '}
            <BotaoRef>Buscar</BotaoRef>. Para cada uma, <BotaoRef>Editar</BotaoRef> → aba{' '}
            <AbaRef>Unidades</AbaRef> → marque a padrão → <BotaoRef>Salvar alterações</BotaoRef>.
          </P>
          <P>
            Enquanto isso não é feito, a pessoa não aparece em unidade nenhuma da Comunicação interna
            (só dá para falar com ela direto) — e qualquer alteração no cadastro dela pede a unidade
            padrão antes de salvar: o formulário abre sozinho a aba Unidades com o aviso.
          </P>
        </>
      ),
    },
    {
      id: 'pela-unidade',
      titulo: 'Incluir pela tela da unidade',
      busca: 'tela da unidade aba usuários incluir remover estrela padrão unidades',
      conteudo: (
        <>
          <P>
            Também dá para gerenciar quem é de uma unidade a partir dela: Cadastros → Unidades → abra a
            unidade → aba <AbaRef>Usuários</AbaRef>. A estrela ao lado do nome marca quem tem aquela
            como unidade padrão.
          </P>
          <Lista>
            <Item>Incluir alguém que ainda não tem unidade padrão faz desta a padrão dele.</Item>
            <Item>
              Remover alguém cuja unidade padrão é esta é recusado: troque a padrão no cadastro do
              usuário primeiro.
            </Item>
          </Lista>
        </>
      ),
    },
    {
      id: 'acesso-global',
      titulo: 'Acesso global',
      busca: 'acesso global todas as unidades sem vínculo conceder revogar administrador',
      conteudo: (
        <>
          <P>
            Quem tem <strong>acesso global</strong> enxerga os dados de todas as unidades, sem depender
            das unidades marcadas. A caixa só aparece, na edição, para quem já tem acesso global — só
            essas pessoas podem dar ou tirar esse acesso de alguém.
          </P>
          <P>
            Mesmo com acesso global a pessoa precisa de unidade padrão: é ela que diz em que equipe
            ela aparece no chat interno.
          </P>
        </>
      ),
    },
    {
      id: 'senha',
      titulo: 'Senha esquecida ou trocada',
      busca: 'esqueci a senha nova senha gerar senha aleatória exibida uma vez trocar no próximo login segurança',
      conteudo: (
        <>
          <P>
            Na edição do usuário, a aba <AbaRef>Dados</AbaRef> termina com a seção{' '}
            <strong>Segurança</strong>, com dois caminhos:
          </P>
          <ListaDefinicoes
            itens={[
              {
                termo: 'Aplicar nova senha',
                descricao:
                  'Você digita a senha (mínimo 8 caracteres). Pode exigir que a pessoa troque no próximo login.',
              },
              {
                termo: 'Gerar nova senha aleatória',
                descricao:
                  'O sistema cria a senha e a mostra uma única vez — copie e entregue à pessoa. Ela sempre terá de trocar no próximo login.',
              },
            ]}
          />
        </>
      ),
    },
    {
      id: 'excluir',
      titulo: 'Excluir',
      busca: 'excluir usuário conta some não entra mais motorista inativo',
      conteudo: (
        <>
          <P>
            <BotaoRef>Excluir</BotaoRef> tira a conta das listagens e a pessoa deixa de conseguir entrar.
            O histórico do que ela fez continua com o nome dela.
          </P>
          <Callout tipo="atencao">
            Não há como desfazer a exclusão por esta tela. Conta de motorista não se exclui aqui — é
            pela tela de Motoristas.
          </Callout>
        </>
      ),
    },
    {
      id: 'quem-pode',
      titulo: 'Quem pode o quê',
      busca: 'permissão módulo usuários consulta inclusão edição exclusão quem pode',
      conteudo: (
        <>
          <P>Tudo aqui é do módulo <strong>Usuários</strong>:</P>
          <ListaDefinicoes
            itens={[
              { termo: 'Consulta', descricao: 'Ver a lista e abrir o cadastro.' },
              {
                termo: 'Inclusão + Edição',
                descricao:
                  'Cadastrar. As duas, porque as unidades (obrigatórias) e as permissões a mais são gravadas logo depois do cadastro, como edição.',
              },
              { termo: 'Edição', descricao: 'Alterar dados, perfis, permissões, unidades e senha.' },
              { termo: 'Exclusão', descricao: 'Excluir a conta.' },
            ]}
          />
          <P>
            Incluir e remover pessoas pela aba Usuários da unidade é do módulo <strong>Unidades</strong>{' '}
            (Edição).
          </P>
        </>
      ),
    },
  ],
};
