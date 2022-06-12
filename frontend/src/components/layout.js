import Header from './header'
import Container from '@mui/material/Container';
import Box from '@mui/material/Box';
import Paper from '@mui/material/Paper';

export default function Layout({ children, title='Mutualify' }) {
  return (
    <>
      <Container maxWidth="lg" sx={{
        marginTop: 4,
        padding: 2
      }}>
        <Paper>
          <Box>
            <Header title={title}/>
            <Box
            sx={{
                marginTop: 1,
                padding: 2
            }}
            >
            {children}
            </Box>
          </Box>
        </Paper>
      </Container>
    </>
  )
}