import * as React from 'react';
import useSWR from 'swr'
import api from '../lib/api'
import User from '../components/user'
import Head from 'next/head'
import Layout from '../components/layout'
import UserContext from '../context/userContext';
import { useContext } from 'react';

export default function Friends() {
  const {
    data: friends,
    error: friendsError,
    isValidating: friendsValidating 
  } = useSWR(`/friends`, api);

  const { user } = useContext(UserContext)
  return (
    <>
      <Head>
        <title>Friend list</title>
      </Head>
      <Layout title="Friend list">
        {!user && (<>Log in first!</>)}
        {user && (<>
          {!friends && friendsValidating && (<>Loading...</>)}
          {!friends && friendsError && friendsError.info && (<>{friendsError.info}</>)}
          {friends && friends.length === 0 && (<>No friends :(</>)}
          {friends && friends.map((data) => (
            <User id={data.id} username={data.username} />
          ))}
        </>)}
      </Layout>
    </>
  );
}