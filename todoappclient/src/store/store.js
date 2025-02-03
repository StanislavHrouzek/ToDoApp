import { createStore } from 'vuex'

export default createStore({
  state: {
    items: []
  },
  mutations: {
    setItems(state, items) {
      state.items = items
    }
  },
  actions: {
    async fetchItems({ commit }) {
      try {
        const response = await fetch('http://localhost:5139/todo')
        if (!response.ok) {
          throw new Error('Network response was not ok')
        }
        const contentType = response.headers.get('content-type')
        if (!contentType || !contentType.includes('application/json')) {
          throw new TypeError('Expected JSON response')
        }
        const data = await response.json()
        commit('setItems', data)
      } catch (error) {
        console.error('Error fetching items:', error)
      }
    },
    async addItem({ dispatch }, item) {
      try {
        const response = await fetch('http://localhost:5139/todo', {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json'
          },
          body: JSON.stringify({
            ...item,
            state: parseInt(item.state) // Ensure state is an integer
          })
        })
        if (!response.ok) {
          throw new Error('Network response was not ok')
        }
        await dispatch('fetchItems')
      } catch (error) {
        console.error('Error adding item:', error)
      }
    },
    async deleteItem({ dispatch }, id) {
      try {
        const response = await fetch(`http://localhost:5139/todo/${id}`, {
          method: 'DELETE',
          headers: {
            'Content-Type': 'application/json'
          }
        })
        if (!response.ok) {
          throw new Error('Network response was not ok')
        }
        await dispatch('fetchItems')
      } catch (error) {
        console.error('Error deleting item:', error)
      }
    },
    async updateItem({ dispatch }, item) {
      try {
        const response = await fetch(`http://localhost:5139/todo/${item.id}`, {
          method: 'PUT',
          headers: {
            'Content-Type': 'application/json'
          },
          body: JSON.stringify({
            ...item,
            state: parseInt(item.state) // Ensure state is an integer
          })
        })
        if (!response.ok) {
          throw new Error('Network response was not ok')
        }
        await dispatch('fetchItems')
      } catch (error) {
        console.error('Error updating item:', error)
      }
    }
  },
  getters: {
    items: state => state.items
  }
})
