import React from 'react';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { StudySessions } from '../../src/components/StudySessions';

// UX fix 4: a rating that was not saved must say so.
describe('UX fix 4: a failed rating is not silent', () => {
  const json = (status, body) => Promise.resolve({ ok: status < 400, status, json: () => Promise.resolve(body) });
  const session = {
    id: 1, course: 'Software Development I', topic: 'Pull request etiquette', location: 'MIF, room 401',
    startsAt: '2030-01-01T16:00:00Z', hostName: 'Ieva', seatsAvailable: 3, status: 'Scheduled', hostRating: null
  };

  beforeEach(() => {
    global.fetch = jest.fn(() => json(200, [session]));
  });

  afterEach(() => {
    delete global.fetch;
  });

  it('says the session no longer exists and refreshes the list when the API answers 404', async () => {
    render(<StudySessions />);
    await screen.findByText('Pull request etiquette');
    global.fetch
      .mockImplementationOnce(() => json(404, { title: 'Not Found' })) // the PUT
      .mockImplementationOnce(() => json(200, []));                    // the list, without the deleted session

    fireEvent.change(screen.getByLabelText('Rate "Pull request etiquette"'), { target: { value: '5' } });

    expect((await screen.findByRole('alert')).textContent).toContain('That session no longer exists');
    await waitFor(() => expect(screen.queryByText('Pull request etiquette')).toBeNull());
  });

  it('says the rating was not saved when the server cannot be reached', async () => {
    render(<StudySessions />);
    await screen.findByText('Pull request etiquette');
    global.fetch.mockImplementationOnce(() => Promise.reject(new TypeError('Failed to fetch')));

    fireEvent.change(screen.getByLabelText('Rate "Pull request etiquette"'), { target: { value: '5' } });

    expect((await screen.findByRole('alert')).textContent).toContain('The rating was not saved');
  });
});
